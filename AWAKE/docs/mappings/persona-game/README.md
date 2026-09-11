# Persona → Bannerlord mapping

- Schema: awake.persona-game-mapping.v1
- AF source: $PersonaDirectory
- Mapping policy: only AF filenames are read for persona names; AF JSON body is not treated as authority.
- Game authority: heroes.xml → spclans.xml → spkingdoms.xml.
- Persona files: 415
- Exact current-game bindings: 362
- Missing current Hero entries: 53

战帆说明：战帆是卡拉迪亚世界的官方 DLC。当前游戏未安装战帆时，缺少对应运行数据只代表本机不可用；它仍可作为同一世界下的 DLC 参考，不应被当成独立世界。

Files:

- persona-game-mapping.v1.json: complete machine-readable mapping with provenance and hashes.
- persona-game-mapping.v1.csv: editor-friendly table.
- persona-game-mapping-missing-current-hero.v1.csv: AF names with no current heroes.xml entry; do not infer their clan automatically.

Important:

- display_name_from_filename is the Chinese name supplied by the AF filename.
- hero_id is the game Hero code when the current XML contains it.
- clan_id and kingdom_id are resolved only through current game XML.
- clan_name_key and kingdom_name_key are native localization keys, not translated display names.
- A future game/mod version requires regenerating this mapping and comparing input hashes.
