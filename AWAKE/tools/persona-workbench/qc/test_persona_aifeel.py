"""Mutation controls for intrinsic (after-only) style readings."""

import unittest
import json
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory

from persona_aifeel import frame_family, metrics, shape


def sample(texts, field="realSelfBehaviors"):
    return [{"card": f"card-{i % 3}", "field": field, "index": i // 3,
             "text": s, "shape": shape(s), "family": frame_family(s)}
            for i, s in enumerate(texts)]


class IntrinsicReadingsTests(unittest.TestCase):
    def test_new_slot_filled_prose_is_detected_without_old_corpus(self):
        same = sample(["进山先看雪，再看脚印", "验账先看数，再问人",
                       "进门先看灯，再看座次", "出兵先问粮，才问路",
                       "拿钱先看印，才开箱", "问事先听完，再回话"])
        diverse = sample(["进山时他会绕开猎人的旧径。", "这笔税并非由她征收。",
                          "他没有答应；人已经散了。", "对方拿走马，他转身去找族长。",
                          "她问的是账簿，不是价钱。", "若城门不开，今晚就留在营中。"])
        a = metrics(same)["realSelfBehaviors"]
        b = metrics(diverse)["realSelfBehaviors"]
        self.assertGreater(a["top_non_other_family_share"], b["top_non_other_family_share"])
        self.assertEqual(a["frame_families"]["first_then"], len(same))
        self.assertEqual(a["first_family_share"], 1)

    def test_uniform_example_posture_is_detected_without_old_corpus(self):
        same = sample(["账簿在这儿。", "路从北门走。", "钱已经点清。", "马今晚留营。"], "selfClaimExamples")
        varied = sample(["账簿在这儿。", "路从北门走！", "钱已经点清？", "马今晚留营；明日再议。"], "selfClaimExamples")
        self.assertEqual(metrics(same)["selfClaimExamples"]["top_ending_share"], 1)
        self.assertLess(metrics(varied)["selfClaimExamples"]["top_ending_share"], 1)

    def test_length_spread_control(self):
        narrow = sample(["这条路今晚不走。", "这笔账今晚不算。", "这扇门今晚不开。"], "selfClaimExamples")
        broad = sample(["走。", "这笔账今晚不算。", "明早把车队逐一清点，等城门开了再向守卫报数。"], "selfClaimExamples")
        self.assertLess(metrics(narrow)["selfClaimExamples"]["length_cv"],
                        metrics(broad)["selfClaimExamples"]["length_cv"])

    def test_draft_text_is_not_loaded(self):
        from tempfile import TemporaryDirectory
        from pathlib import Path
        from persona_aifeel import rows_from_definitions
        with TemporaryDirectory() as temp:
            p = Path(temp) / "a.definition.json"
            p.write_text('{"core":"甲","tensionAxes":{"hardLine":"先甲，再乙"}}', encoding="utf-8")
            rows = rows_from_definitions(temp)
            self.assertEqual([r["field"] for r in rows], ["core"])

    def test_pronoun_is_review_flag_not_false_hard_failure(self):
        r = metrics(sample(["你先把伤者送进去。"], "selfClaimExamples"))
        self.assertEqual(r["review_flags"][0]["reason"], "second_person_in_example")

    def test_fallback_gate_is_not_a_style_pass(self):
        from persona_aifeel import dsl_usable
        self.assertFalse(dsl_usable("[PERSONA_DSL_OK] fallback=True\nWARN persona.tag_unregistered:x"))
        self.assertFalse(dsl_usable("[PERSONA_DSL_OK] fallback=False\nWARN persona.tag_unregistered:x"))
        self.assertTrue(dsl_usable("[PERSONA_DSL_OK] fallback=False"))

    def test_actual_csharp_renderer_is_single_pass_json_quoted(self):
        from persona_aifeel import REPO
        exe = REPO / "tools/worldbook-runtime-production-smoke/artifacts/bin/Release/Awake.WorldbookRuntimeProductionSmoke.exe"
        if not exe.is_file():
            self.skipTest("build production smoke first")
        with TemporaryDirectory() as temp:
            variables, rendered = Path(temp) / "vars.json", Path(temp) / "prompt.txt"
            values = {key: "" for key in ("retrieved_knowledge", "npc_memory", "npc_identity",
                      "persona_dsl", "npc_state", "npc_commitments", "player_known", "scene",
                      "opening_hint", "player_turn", "npc_id", "dialogue_action_mode", "dialogue_history")}
            values.update({"npc_identity": '甲"乙', "player_turn": "{{npc_id}}"})
            variables.write_text(json.dumps(values, ensure_ascii=False), encoding="utf-8")
            run = subprocess.run([str(exe), "persona-prompt-render", str(variables), str(rendered)],
                                 capture_output=True, text=True)
            self.assertEqual(run.returncode, 0, run.stderr)
            text = rendered.read_text(encoding="utf-8")
            self.assertIn('甲\\"乙', text)
            self.assertIn('{{npc_id}}', text)

    def test_answer_audit_excludes_before_and_counts_invalid_json(self):
        from persona_aifeel import answer_metrics
        with TemporaryDirectory() as temp:
            path = Path(temp) / "answers.json"
            path.write_text(json.dumps({"model": "fixture", "results": [
                {"version": "before", "stem": "A", "scenario": "one", "sample": 0,
                 "raw": '{"reply":"old"}'},
                {"version": "after", "stem": "A", "scenario": "one", "sample": 0,
                 "raw": '{"reply":"new"}'},
                {"version": "after", "stem": "B", "scenario": "one", "sample": 0,
                 "raw": "not JSON"}]}, ensure_ascii=False), encoding="utf-8")
            audit = answer_metrics(path)
            self.assertEqual(audit["valid"], 1)
            self.assertEqual(len(audit["invalid"]), 1)
            self.assertEqual(audit["by_scenario"]["one"]["n"], 1)

    def test_reply_parser_rejects_plain_text_but_accepts_json_fence(self):
        from persona_aifeel import parse_reply
        self.assertEqual(parse_reply('```json\n{"reply":"是。"}\n```'), "是。")
        with self.assertRaises(json.JSONDecodeError):
            parse_reply("是。")

    def test_place_link_does_not_authorize_boundary_action(self):
        from persona_aifeel import relation_report
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "a.definition.json").write_text(json.dumps({
                "characterId": "hero-a", "realSelfBehaviors": ["她派人征收税粮。"]}, ensure_ascii=False), encoding="utf-8")
            link = root / "links.json"
            link.write_text(json.dumps({"links": [{"heroId": "hero-a", "state": "confirmed",
                                                   "confirmedFor": "place"}]}), encoding="utf-8")
            card = relation_report(root, link)["cards"][0]
            self.assertTrue(card["place_only"])
            self.assertEqual(card["boundary_lines_to_verify"][0]["text"], "她派人征收税粮。")


if __name__ == "__main__":
    unittest.main()
