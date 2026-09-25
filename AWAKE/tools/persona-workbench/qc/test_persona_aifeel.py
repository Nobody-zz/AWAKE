"""Mutation controls for intrinsic (after-only) style readings."""

import unittest
import json
import hashlib
import re
import sys
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory

from persona_aifeel import (SCENARIOS, build_unknown_fact_review_queue, check_claim_retention,
                            find_ollama_model_tag, frame_family, metrics, ollama_error_detail,
                            reply_run_status, rows_from_dsl, sample_seeds, shape,
                            validate_required_claims)


def sample(texts, field="realSelfBehaviors"):
    return [{"card": f"card-{i % 3}", "field": field, "index": i // 3,
             "text": s, "shape": shape(s), "family": frame_family(s)}
            for i, s in enumerate(texts)]


class IntrinsicReadingsTests(unittest.TestCase):
    def test_chain_fixture_has_four_distinct_situations_including_unknown_fact(self):
        self.assertEqual(set(SCENARIOS), {"identity", "boundary", "challenge", "unknown"})
        self.assertIn("确凿消息", SCENARIOS["unknown"])

    def test_production_spec_allows_simple_supported_scope_without_waiving_overclaims(self):
        workbench = Path(__file__).resolve().parents[1]
        spec = (workbench / "PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md").read_text(encoding="utf-8")
        fixtures = (workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-FIXTURES-20260925.md").read_text(encoding="utf-8")
        answer_key = (workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-ANSWER-KEY-20260925.md").read_text(encoding="utf-8")
        review_form = (workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-FORM-20260925.md").read_text(encoding="utf-8")
        blind_form = (workbench / "qc" / "PERSONA-BLIND-REVIEW-TEMPLATE.zh-CN.md").read_text(encoding="utf-8")
        author_record = (workbench / "qc" / "PERSONA-AUTHORING-RECORD-TEMPLATE.zh-CN.md").read_text(encoding="utf-8")
        review_pack = (workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-PACK-20260925.md").read_text(encoding="utf-8")

        self.assertIn("可取得范围内 `PASS`", spec)
        self.assertIn("不必补第二个行为", spec)
        self.assertIn("不得宣称完整人格认证", spec)
        self.assertIn("## 夹具 D：简单但有据的有限范围卡", fixtures)
        self.assertIn("## 夹具 F：窄范围许可不能掩盖超范围主张", fixtures)
        self.assertIn("F 的额外主张背书", fixtures)
        self.assertIn("| F：窄范围许可不能掩盖超范围主张 | `REVISE`", answer_key)
        self.assertIn("在声明范围内取得 `PASS` / `approved` 的资格", review_form)
        self.assertIn("本夹具材料是否足以直接将卡标为 `approved`", review_form)
        self.assertIn("声明范围有实质重叠的卡；可为零张", blind_form)
        self.assertIn("NOT_APPLICABLE_WITHIN_DECLARED_SCOPE", blind_form)
        self.assertIn("卡片 SHA-256（绑定本次批准范围）", author_record)
        self.assertNotIn("至少两个情境草图", spec)
        self.assertNotIn("0–1 → `INSUFFICIENT_EVIDENCE`", spec)
        self.assertIn("当前实现只报告 O2–O5", spec)
        self.assertIn("O6/M9 尚未进入脚本", spec)
        self.assertIn("相同的五份文件", review_pack)
        self.assertNotIn("相同的三份文件", review_pack)

    def test_production_spec_review_pack_hashes_match_exact_inputs(self):
        workbench = Path(__file__).resolve().parents[1]
        manifest = (workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-PACK-20260925.md").read_text(encoding="utf-8")
        inputs = (
            workbench / "PERSONA-CARD-PRODUCTION-SPEC.zh-CN.md",
            workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-FORM-20260925.md",
            workbench / "qc" / "PERSONA-PRODUCTION-SPEC-REVIEW-FIXTURES-20260925.md",
            workbench / "qc" / "PERSONA-AUTHORING-RECORD-TEMPLATE.zh-CN.md",
            workbench / "qc" / "PERSONA-BLIND-REVIEW-TEMPLATE.zh-CN.md",
        )
        for path in inputs:
            with self.subTest(path=path.name):
                match = re.search(rf"`{re.escape(path.name)}`\s*\|\s*`([0-9a-f]{{64}})`", manifest)
                self.assertIsNotNone(match, f"missing hash lock for {path.name}")
                actual = hashlib.sha256(path.read_bytes().replace(b"\r\n", b"\n")).hexdigest()
                self.assertEqual(match.group(1), actual)

    def test_repeat_samples_have_stable_one_based_ids_and_distinct_seeds(self):
        self.assertEqual(sample_seeds(1700, 2), [(1, 1700), (2, 1701)])
        self.assertEqual(sample_seeds(20, 1), [(1, 20)])
        for invalid in (0, 6, True, 1.5):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                sample_seeds(0, invalid)

    def test_partial_or_invalid_model_batch_cannot_be_marked_scored(self):
        self.assertEqual(reply_run_status(24, 0, 24), "scored")
        self.assertEqual(reply_run_status(23, 1, 24), "incomplete")
        self.assertEqual(reply_run_status(0, 24, 24), "no_valid_replies")

    def test_model_metadata_uses_exact_tag_and_digest(self):
        tags = {"models": [{"name": "qwen2.5:latest", "digest": "abc123", "size": 42},
                           {"name": "qwen2.5:7b", "digest": "other", "size": 9}]}
        self.assertEqual(find_ollama_model_tag(tags, "qwen2.5:latest"),
                         {"name": "qwen2.5:latest", "digest": "abc123", "size": 42})
        self.assertIsNone(find_ollama_model_tag(tags, "missing"))
        self.assertIsNone(find_ollama_model_tag({"models": "invalid"}, "qwen2.5:latest"))

    def test_http_error_report_preserves_ollama_diagnostic_body(self):
        import urllib.error
        from io import BytesIO
        error = urllib.error.HTTPError("http://localhost", 500, "Internal Server Error", {},
                                       BytesIO(b'{"error":"insufficient memory"}'))
        self.assertEqual(ollama_error_detail(error), 'HTTP 500: {"error":"insufficient memory"}')

    def test_unknown_fact_samples_are_always_queued_for_human_grounding_review(self):
        item = {"card": "anon-A", "prompts": {"unknown": [
            {"sample": 1, "seed": 12, "sha256": "prompt-hash", "raw":
             '{"reply":"某家族截了盐运。","mood":"谨慎"}'},
            {"sample": 2, "seed": 13, "sha256": "prompt-hash-2", "raw":
             '{"reply":"我没有确凿消息。","mood":"平静"}'}]}}
        queue = build_unknown_fact_review_queue([item], 2)
        self.assertEqual(queue["status"], "needs_human_review")
        self.assertEqual(queue["queued"], 2)
        self.assertEqual(queue["items"][0]["reply"], "某家族截了盐运。")
        self.assertEqual(queue["items"][0]["status"], "needs_manual_grounding_review")
        self.assertEqual(build_unknown_fact_review_queue([item], 3)["status"], "incomplete")

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

    def test_synonym_swaps_do_not_erase_the_sequence_family(self):
        disguised = sample(["进山首先看雪，随后查脚印", "验账第一步看数，接着问来人",
                            "进门起初看灯，然后看座次", "出兵先问粮，才问路"])
        reading = metrics(disguised)["realSelfBehaviors"]
        self.assertEqual(reading["first_family_share"], 1)

    def test_only_not_frame_is_detected_as_a_locator_not_a_quality_verdict(self):
        repeated = sample(["她只查印，不听口头保证。", "他只认粮单，不认传闻。",
                           "此人只问来路，不问价钱。"])
        varied = sample(["她只查印，不听口头保证。", "他把粮单收进柜中。",
                         "来客尚未坐稳，主人便递来一杯水。"])
        repeated_reading = metrics(repeated)["realSelfBehaviors"]
        varied_reading = metrics(varied)["realSelfBehaviors"]
        self.assertEqual(repeated_reading["frame_families"]["only_not"], 3)
        self.assertEqual(repeated_reading["top_non_other_family_share"], 1)
        self.assertLess(varied_reading["top_non_other_family_share"],
                        repeated_reading["top_non_other_family_share"])
        self.assertNotIn("verdict", repeated_reading)

    def test_only_not_locator_excludes_not_only_also_and_only_if_not_frames(self):
        self.assertNotEqual(frame_family("他不只查账，也核验印章。"), "only_not")
        self.assertNotEqual(frame_family("只要不下雨，车队就能出发。"), "only_not")

    def test_different_meanings_with_same_sentence_frame_remain_a_locator_not_a_verdict(self):
        lines = ["她先核对盟约，再拒绝出兵。", "他先核对粮草，再接受有限支援。"]
        reading = metrics(sample(lines))["realSelfBehaviors"]
        self.assertEqual(reading["first_family_share"], 1)
        self.assertNotIn("verdict", reading)

    def test_evidence_limited_simple_dsl_is_valid_without_filling_optional_personality_sections(self):
        dsl = """[PERSONA_IDENTITY]
DATA_CN="在世的领主。"
[PERSONALITY_CORE]
DATA_CN="说话寡少。"
"""
        rows = rows_from_dsl(dsl, "anonymous-D")
        self.assertEqual(len(rows), 2)
        self.assertEqual({row["field"] for row in rows}, {"PERSONA_IDENTITY", "PERSONALITY_CORE"})
        self.assertEqual(metrics(rows, fields=sorted({row["field"] for row in rows}))["PERSONALITY_CORE"]["n"], 1)

    def test_real_generator_and_template_accept_a_sparse_evidence_limited_definition(self):
        from persona_aifeel import REPO
        simulator = REPO / "tools/worldbook-runtime-sim/bin/Release/net10.0-windows/WorldbookRuntimeSim.dll"
        renderer = REPO / "tools/worldbook-runtime-production-smoke/artifacts/bin/Release/Awake.WorldbookRuntimeProductionSmoke.exe"
        registry = REPO / "ModuleData/Worldbook/persona_definitions/tag_registry.json"
        if not simulator.is_file() or not renderer.is_file() or not registry.is_file():
            self.skipTest("build Persona simulator and production-smoke first")
        identity_claim, core_claim = "身份档案只记载其为某家族成员。", "他很少谈自己的经历。"
        definition = {
            "schemaVersion": "awake.persona.character.v1", "id": "redteam.sparse.fixture",
            "characterId": "redteam_sparse_fixture", "identityId": "fixture", "role": "hero",
            "sourcePackId": "redteam", "templateVersion": "persona-load.v2", "status": "approved",
            "priority": 1, "scope": "character", "core": core_claim,
            "identityFacts": identity_claim, "tags": []
        }
        with TemporaryDirectory() as temp:
            root = Path(temp)
            defs, dsl_path = root / "definitions", root / "sparse.dsl.txt"
            defs.mkdir()
            (defs / "sparse.definition.json").write_text(
                json.dumps(definition, ensure_ascii=False), encoding="utf-8")
            result = subprocess.run(
                ["dotnet", str(simulator), "persona", str(defs), str(registry),
                 "redteam_sparse_fixture", str(dsl_path), "6144"],
                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(result.returncode, 0, (result.stderr or "") + (result.stdout or ""))
            self.assertIn("fallback=False", result.stdout)
            self.assertIn("trimmed=False", result.stdout)
            dsl = dsl_path.read_text(encoding="utf-8")
            self.assertIn(identity_claim, dsl)
            self.assertIn(core_claim, dsl)

            variable_names = ("retrieved_knowledge", "npc_memory", "npc_identity", "persona_dsl",
                              "npc_state", "npc_commitments", "player_known", "scene", "opening_hint",
                              "player_turn", "npc_id", "dialogue_action_mode", "dialogue_history")
            variables = {name: "" for name in variable_names}
            variables.update({"npc_identity": "匿名角色", "persona_dsl": dsl,
                              "player_turn": "你是谁？", "dialogue_action_mode": "chat"})
            variable_path, prompt_path = root / "vars.json", root / "rendered.txt"
            variable_path.write_text(json.dumps(variables, ensure_ascii=False), encoding="utf-8")
            rendered = subprocess.run(
                [str(renderer), "persona-prompt-render", str(variable_path), str(prompt_path)],
                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(rendered.returncode, 0, (rendered.stderr or "") + (rendered.stdout or ""))
            prompt = prompt_path.read_text(encoding="utf-8")
            self.assertIn(identity_claim, prompt)
            self.assertIn(core_claim, prompt)
            retained = check_claim_retention(
                [{"id": "identity", "text": identity_claim}, {"id": "core", "text": core_claim}],
                dsl, prompt, "sparse.definition.json")
            self.assertEqual(retained["status"], "pass")
            self.assertTrue(retained["rendered_dsl_present"])
            blocked = check_claim_retention(
                [{"id": "identity", "text": identity_claim}], dsl,
                prompt.replace(json.dumps(dsl, ensure_ascii=False), ""), "sparse.definition.json")
            self.assertEqual(blocked["status"], "blocked")
            self.assertEqual(blocked["claims"][0]["status"], "rendered_dsl_not_found")

    def test_minimal_evidence_card_survives_full_materialize_dsl_and_prompt_chain(self):
        from persona_aifeel import REPO
        script = REPO / "tools/persona-workbench/qc/persona_aifeel.py"
        with TemporaryDirectory() as temp:
            root = Path(temp)
            card = root / "simple.persona.json"
            sidecar = root / "simple.origins.json"
            claims = root / "claims.json"
            report_path = root / "report.json"
            identity_claim = "来源记录确认此人为城堡 X 的守卫队长。"
            behavior_claim = "公开记录写明，她遇到陌生来客时先询问来意。"
            card.write_text(json.dumps({
                "schemaVersion": "persona-workbench.character.v1",
                "id": "calradia.redteam.simple_evidence",
                "displayName": "匿名守卫队长",
                "core": "",
                "identityFacts": identity_claim,
                "summary": "",
                "sourceDescription": "仅有身份记录和一条公开行为记录。",
                "publicDescription": "",
                "privateDescription": "",
                "contradictionDescription": "",
                "selfClaimRules": [],
                "realSelfBehaviors": [behavior_claim],
                "selfClaimExamples": [],
                "tags": [],
                "status": "approved",
                "sourcePackId": "redteam.fixture",
                "templateVersion": "redteam-simple.v1"
            }, ensure_ascii=False), encoding="utf-8")
            sidecar.write_text(json.dumps({"heroId": "redteam_simple_evidence"}), encoding="utf-8")
            claims.write_text(json.dumps({
                "schemaVersion": "persona-aifeel.required-claims.v1",
                "cards": {card.name: [
                    {"id": "C001", "text": identity_claim},
                    {"id": "C002", "text": behavior_claim}
                ]}
            }, ensure_ascii=False), encoding="utf-8")

            run = subprocess.run([
                sys.executable, str(script), "--out", str(report_path), "--quiet", "chain",
                str(card), "--samples", "1", "--claims", str(claims)
            ], capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
            self.assertEqual(run.returncode, 0, run.stderr + run.stdout)
            report = json.loads(report_path.read_text(encoding="utf-8"))
            self.assertTrue(report["force_approved_for_offline_test"])
            self.assertIsNone(report["model"])
            self.assertEqual(len(report["items"]), 1)
            item = report["items"][0]
            self.assertIn("fallback=False", item["dsl_log"])
            self.assertIn("trimmed=False", item["dsl_log"])
            self.assertIn(identity_claim, item["dsl"])
            self.assertIn(behavior_claim, item["dsl"])
            self.assertEqual(set(item["prompts"]), set(SCENARIOS))
            for rendered_samples in item["prompts"].values():
                self.assertEqual(len(rendered_samples), 1)
                rendered = rendered_samples[0]
                self.assertIn(identity_claim, rendered["prompt"])
                self.assertIn(behavior_claim, rendered["prompt"])
                self.assertEqual(rendered["claim_retention"]["status"], "pass")

    def test_required_claim_manifest_is_strict_and_card_scoped(self):
        with TemporaryDirectory() as temp:
            path = Path(temp) / "claims.json"
            path.write_text(json.dumps({"schemaVersion": "persona-aifeel.required-claims.v1",
                                       "cards": {"a.persona.json": [{"id": "C1", "text": "确切主张"}]}}),
                            encoding="utf-8")
            self.assertEqual(validate_required_claims(path, ["a.persona.json"])["a.persona.json"][0]["id"], "C1")
            with self.assertRaisesRegex(ValueError, "not selected"):
                validate_required_claims(path, ["b.persona.json"])
            with self.assertRaisesRegex(ValueError, "explicitly include every selected card"):
                validate_required_claims(path, ["a.persona.json", "b.persona.json"])
            path.write_text(json.dumps({"schemaVersion": "persona-aifeel.required-claims.v1",
                                        "cards": {"a.persona.json": [{"id": "C1", "text": "确切主张"}],
                                                  "b.persona.json": []}}), encoding="utf-8")
            self.assertEqual(validate_required_claims(path, ["a.persona.json", "b.persona.json"])
                             ["b.persona.json"], [])

    def test_empty_required_claims_still_require_dsl_in_final_prompt(self):
        result = check_claim_retention([], "[PERSONA_IDENTITY]\nDATA_CN=\"身份\"\n", "没有 DSL")
        self.assertEqual(result["status"], "blocked")
        self.assertFalse(result["rendered_dsl_present"])

    def test_real_generator_reports_trim_and_drops_optional_required_claim(self):
        from persona_aifeel import REPO
        simulator = REPO / "tools/worldbook-runtime-sim/bin/Release/net10.0-windows/WorldbookRuntimeSim.dll"
        registry = REPO / "ModuleData/Worldbook/persona_definitions/tag_registry.json"
        if not simulator.is_file() or not registry.is_file():
            self.skipTest("build WorldbookRuntimeSim Release first")
        sentinel = "REQUIRED_CLAIM_SENTINEL_7F42"
        definition = {
            "schemaVersion": "awake.persona.character.v1", "id": "redteam.trim.fixture",
            "characterId": "redteam_trim_fixture", "identityId": "fixture", "role": "hero",
            "sourcePackId": "redteam", "templateVersion": "persona-load.v2", "status": "approved",
            "priority": 1, "scope": "character", "core": "核心" * 4000,
            "identityFacts": "测试身份", "publicDescription": sentinel, "tags": []
        }
        with TemporaryDirectory() as temp:
            root = Path(temp)
            defs, output = root / "definitions", root / "fixture.dsl.txt"
            defs.mkdir()
            (defs / "fixture.definition.json").write_text(
                json.dumps(definition, ensure_ascii=False), encoding="utf-8")
            result = subprocess.run(
                ["dotnet", str(simulator), "persona", str(defs), str(registry),
                 "redteam_trim_fixture", str(output), "6144"],
                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(result.returncode, 0, (result.stderr or "") + (result.stdout or ""))
            self.assertIn("fallback=False", result.stdout)
            self.assertIn("trimmed=True", result.stdout)
            self.assertNotIn(sentinel, output.read_text(encoding="utf-8"))

    def test_one_prolific_card_does_not_masquerade_as_cross_card_sameness(self):
        rows = ([{"card": "one", "field": "realSelfBehaviors", "index": i, "text": "先查账，再问人",
                  "shape": shape("先查账，再问人"), "family": frame_family("先查账，再问人")}
                 for i in range(8)] +
                [{"card": "two", "field": "realSelfBehaviors", "index": 0, "text": "她转身离开。",
                  "shape": shape("她转身离开。"), "family": frame_family("她转身离开。")},
                 {"card": "three", "field": "realSelfBehaviors", "index": 0, "text": "他已经签了。",
                  "shape": shape("他已经签了。"), "family": frame_family("他已经签了。")}])
        reading = metrics(rows)["realSelfBehaviors"]
        self.assertGreater(reading["first_family_share"], 0.5)
        self.assertEqual(reading["family_card_coverage"]["first_then"], 1)

    def test_single_card_readings_are_marked_inadequate_for_cross_card_claims(self):
        reading = metrics([{"card": "only", "field": "realSelfBehaviors", "index": 0,
                            "text": "先查账，再问人", "shape": shape("先查账，再问人"),
                            "family": frame_family("先查账，再问人")}])["realSelfBehaviors"]
        self.assertTrue(reading["insufficient_cross_card_sample"])

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

    def test_runtime_metrics_only_read_values_present_in_emitted_dsl(self):
        dsl = """[PERSONALITY_CORE]
TRAIT_PROUD
DATA_CN="甲\\n说\\\"话"
[PERSONALITY_PUBLIC]
DATA_CN="先看账，再问人"
"""
        rows = rows_from_dsl(dsl, "anonymous-A")
        self.assertEqual([row["field"] for row in rows],
                         ["PERSONALITY_CORE", "PERSONALITY_PUBLIC"])
        self.assertEqual(rows[0]["text"], '甲\n说"话')
        self.assertEqual(rows[1]["text"], "先看账，再问人")
        report = metrics(rows, fields=sorted({row["field"] for row in rows}))
        self.assertNotIn("summary", report)

    def test_malformed_emitted_dsl_data_fails_closed(self):
        with self.assertRaisesRegex(ValueError, "outside a DSL section"):
            rows_from_dsl('DATA_CN="孤立文本"')
        with self.assertRaisesRegex(ValueError, "invalid DATA_CN"):
            rows_from_dsl("""[PERSONALITY_CORE]
DATA_CN=not-json""")

    def test_pronoun_is_review_flag_not_false_hard_failure(self):
        r = metrics(sample(["你先把伤者送进去。"], "selfClaimExamples"))
        self.assertEqual(r["review_flags"][0]["reason"], "second_person_in_example")

    def test_fallback_gate_is_not_a_style_pass(self):
        from persona_aifeel import dsl_usable
        self.assertFalse(dsl_usable("[PERSONA_DSL_OK] fallback=True\nWARN persona.tag_unregistered:x"))
        self.assertFalse(dsl_usable("[PERSONA_DSL_OK] fallback=False\nWARN persona.tag_unregistered:x"))
        self.assertFalse(dsl_usable("[PERSONA_DSL_OK] fallback=True\nsource=C:/fallback=False/card.json"))
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
                 "raw": '{"reply":"old","mood":"平静"}'},
                {"version": "after", "stem": "A", "scenario": "one", "sample": 0,
                 "raw": '{"reply":"new","mood":"平静"}'},
                {"version": "after", "stem": "B", "scenario": "one", "sample": 0,
                 "raw": "not JSON"}]}, ensure_ascii=False), encoding="utf-8")
            audit = answer_metrics(path)
            self.assertEqual(audit["valid"], 1)
            self.assertEqual(len(audit["invalid"]), 1)
            self.assertEqual(audit["by_scenario"]["one"]["n"], 1)

    def test_null_raw_is_counted_invalid_instead_of_crashing_error_report(self):
        from persona_aifeel import answer_metrics
        with TemporaryDirectory() as temp:
            path = Path(temp) / "answers.json"
            path.write_text(json.dumps({"model": "fixture", "results": [
                {"version": "after", "stem": "A", "scenario": "one", "sample": 0,
                 "raw": '{"reply":"是。","mood":"平静"}'},
                {"version": "after", "stem": "B", "scenario": "one", "sample": 0, "raw": None}]
            }, ensure_ascii=False), encoding="utf-8")
            self.assertEqual(len(answer_metrics(path)["invalid"]), 1)

    def test_all_invalid_answers_still_produce_a_diagnostic_report(self):
        from persona_aifeel import answer_metrics
        with TemporaryDirectory() as temp:
            path = Path(temp) / "answers.json"
            path.write_text(json.dumps({"model": "fixture", "results": [
                {"version": "after", "stem": "A", "scenario": "one", "sample": 0,
                 "raw": '```json\n{"reply":"是。","mood":"平静"}\n```'}]
            }, ensure_ascii=False), encoding="utf-8")
            report = answer_metrics(path)
            self.assertEqual(report["status"], "no_valid_replies")
            self.assertEqual(report["valid"], 0)
            self.assertEqual(len(report["invalid"]), 1)
            self.assertEqual(report["diagnostic_only"]["n"], 1)
            self.assertEqual(report["diagnostic_only"]["by_scenario"]["one"]["n"], 1)
            output = Path(temp) / "report.json"
            import sys
            cli = subprocess.run([sys.executable, str(Path(__file__).with_name("persona_aifeel.py")),
                                  "--out", str(output), "answers", str(path)], capture_output=True, text=True)
            self.assertEqual(cli.returncode, 2, cli.stderr)
            self.assertEqual(json.loads(output.read_text(encoding="utf-8"))["status"], "no_valid_replies")

    def test_reply_parser_rejects_plain_text_and_code_fences(self):
        from persona_aifeel import parse_reply
        self.assertEqual(parse_reply('{"reply":"是。","mood":"平静"}'), "是。")
        with self.assertRaises((json.JSONDecodeError, ValueError)):
            parse_reply('```json\n{"reply":"是。","mood":"平静"}\n```')
        with self.assertRaises(json.JSONDecodeError):
            parse_reply("是。")
        with self.assertRaises((KeyError, ValueError)):
            parse_reply(json.dumps({"reply": "是。"}, ensure_ascii=False))
        with self.assertRaises(ValueError):
            parse_reply(json.dumps({"reply": "是。", "mood": "平静", "command": {}}, ensure_ascii=False))

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

    def test_chain_rejects_missing_sidecar_and_colliding_card_names(self):
        from persona_aifeel import validate_card_paths
        with TemporaryDirectory() as temp:
            root = Path(temp)
            a, b = root / "a", root / "b"
            a.mkdir()
            b.mkdir()
            card_a, card_b = a / "same.persona.json", b / "same.persona.json"
            card_a.write_text("{}", encoding="utf-8")
            card_b.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Duplicate card basenames"):
                validate_card_paths([card_a, card_b])
            with self.assertRaisesRegex(ValueError, "Missing hero identity sidecar"):
                validate_card_paths([card_a])

    def test_malformed_boundary_field_is_not_silently_counted_as_characters(self):
        from persona_aifeel import rows_from_definitions, relation_report
        with TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "a.definition.json").write_text(json.dumps({
                "characterId": "hero-a", "realSelfBehaviors": "先看账，再收税"}, ensure_ascii=False), encoding="utf-8")
            link = root / "links.json"
            link.write_text('{"links":[]}', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "realSelfBehaviors"):
                rows_from_definitions(root)
            with self.assertRaisesRegex(ValueError, "realSelfBehaviors"):
                relation_report(root, link)


if __name__ == "__main__":
    unittest.main()
