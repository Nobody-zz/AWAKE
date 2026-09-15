# 骑砍原版 Prefab 尺寸与间距统计（只读扫描）

> 来源：`D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord`
> 模块：Native、SandBox、SandBoxCore、StoryMode、Multiplayer
> 生成脚本：`AWAKE/docs/scan_native_prefab_metrics.py`（可重跑）

## 1. 总览

- Prefab 文件数：**430**
- 布局控件总数（带 SizePolicy 的元素）：**11026**

## 2. 尺寸策略组合频次（Top 10）

| WidthSizePolicy | HeightSizePolicy | 次数 |
|---|---|---|
| Fixed | Fixed | 3506 |
| StretchToParent | StretchToParent | 2290 |
| CoverChildren | CoverChildren | 2009 |
| StretchToParent | CoverChildren | 1155 |
| StretchToParent | Fixed | 632 |
| Fixed | StretchToParent | 606 |
| Fixed | CoverChildren | 302 |
| CoverChildren | StretchToParent | 289 |
| CoverChildren | Fixed | 188 |
| - | StretchToParent | 24 |

## 3. 控件类型频次（Top 15）

| 控件 | 次数 |
|---|---|
| `Widget` | 3247 |
| `TextWidget` | 1577 |
| `ListPanel` | 1495 |
| `ButtonWidget` | 694 |
| `BrushWidget` | 611 |
| `HintWidget` | 503 |
| `ImageWidget` | 236 |
| `RichTextWidget` | 199 |
| `InputKeyVisualWidget` | 198 |
| `NavigatableListPanel` | 182 |
| `ScrollablePanel` | 147 |
| `ImageIdentifierWidget` | 144 |
| `ScrollbarWidget` | 125 |
| `MaskedTextureWidget` | 113 |
| `SortButtonWidget` | 67 |

## 4. 间距使用习惯

水平间距（MarginLeft / MarginRight 合并）与垂直间距（MarginTop / MarginBottom 合并）：

### 4.1 水平间距 Top 25

| 值 | 次数 |
|---|---|
| 5 | 526 |
| 10 | 437 |
| 15 | 227 |
| 20 | 206 |
| 3 | 135 |
| 2 | 132 |
| 25 | 119 |
| 60 | 100 |
| 30 | 90 |
| 50 | 81 |
| 40 | 78 |
| 4 | 70 |
| 100 | 62 |
| 7 | 58 |
| 8 | 57 |
| 0 | 50 |
| 6 | 47 |
| 45 | 40 |
| 35 | 40 |
| 80 | 30 |
| 55 | 23 |
| 12 | 22 |
| 9 | 22 |
| 13 | 21 |
| -2 | 21 |

### 4.2 垂直间距 Top 25

| 值 | 次数 |
|---|---|
| 5 | 457 |
| 10 | 429 |
| 15 | 295 |
| 20 | 204 |
| 2 | 130 |
| 3 | 128 |
| 25 | 108 |
| 0 | 104 |
| 30 | 90 |
| 50 | 83 |
| 6 | 68 |
| 40 | 66 |
| 100 | 50 |
| 35 | 49 |
| 7 | 46 |
| 8 | 44 |
| 4 | 39 |
| 60 | 33 |
| 12 | 30 |
| 1 | 27 |
| 18 | 25 |
| 80 | 24 |
| 75 | 21 |
| 70 | 19 |
| 120 | 18 |

台阶分析（不含 0）：

| 维度 | 总取值数 | 5的倍数占比 | 10的倍数占比 | 4的倍数占比 | 2的倍数占比 | 最小值 | 最大值 |
|---|---|---|---|---|---|---|---|
| 水平 | 134 | 55.2 | 36.6 | 31.3 | 60.4 | -36 | 1465 |
| 垂直 | 151 | 54.3 | 35.1 | 31.1 | 62.3 | -85 | 975 |

## 5. 建议尺寸使用习惯

### 5.1 SuggestedWidth Top 25

| 值 | 次数 |
|---|---|
| 20 | 156 |
| 8 | 143 |
| 50 | 121 |
| 45 | 118 |
| 100 | 108 |
| 40 | 100 |
| 4 | 89 |
| 60 | 77 |
| 30 | 73 |
| 35 | 72 |
| 400 | 63 |
| 25 | 53 |
| 125 | 49 |
| 250 | 48 |
| 10 | 43 |
| 150 | 43 |
| 350 | 39 |
| 75 | 39 |
| 300 | 37 |
| 90 | 37 |
| 70 | 36 |
| 2 | 35 |
| 200 | 34 |
| 33 | 32 |
| 14 | 32 |

### 5.2 SuggestedHeight Top 25

| 值 | 次数 |
|---|---|
| 40 | 214 |
| 20 | 206 |
| 50 | 200 |
| 30 | 151 |
| 35 | 136 |
| 60 | 116 |
| 45 | 114 |
| 10 | 104 |
| 25 | 79 |
| 55 | 65 |
| 38 | 63 |
| 70 | 59 |
| 100 | 57 |
| 2 | 56 |
| 42 | 51 |
| 65 | 50 |
| 24 | 50 |
| 64 | 42 |
| 150 | 41 |
| 36 | 41 |
| 48 | 40 |
| 75 | 37 |
| 69 | 37 |
| 80 | 34 |
| 32 | 34 |


## 6. 各 Prefab 的最外层固定尺寸面板

共 **370** 个 Prefab 的最外层是「固定尺寸」面板。先看尺寸分布：

### 6.1 面板宽度分布 Top 20

| 宽 | 次数 |
|---|---|
| 40 | 15 |
| 50 | 13 |
| 342 | 11 |
| 8 | 11 |
| 300 | 9 |
| 200 | 9 |
| 45 | 9 |
| 20 | 9 |
| 105 | 8 |
| 694 | 7 |
| 80 | 7 |
| 35 | 7 |
| 500 | 6 |
| 100 | 6 |
| 70 | 6 |
| 600 | 5 |
| 576 | 5 |
| 540 | 5 |
| 60 | 5 |
| 450 | 4 |

### 6.2 面板高度分布 Top 20

| 高 | 次数 |
|---|---|
| 50 | 25 |
| 40 | 18 |
| 20 | 14 |
| 69 | 11 |
| 1080 | 10 |
| 10 | 10 |
| 35 | 9 |
| 80 | 9 |
| 126 | 8 |
| 200 | 7 |
| 55 | 7 |
| 45 | 7 |
| 70 | 7 |
| 30 | 6 |
| 100 | 6 |
| 700 | 5 |
| 900 | 5 |
| 88 | 5 |
| 14 | 5 |
| 923 | 5 |

### 6.3 面板清单（按宽度降序）

| 模块/文件 | 宽 | 高 | 深度 |
|---|---|---|---|
| `Native/LoadingWindow.xml` | 1920 | 1080 | 4 |
| `Native/ProfileSelectionScreen.xml` | 1920 | 1080 | 4 |
| `Native/Standard/Standard.Background.xml` | 1920 | 700 | 4 |
| `Multiplayer/Scoreboard/MultiplayerScoreboard.xml` | 1662 | 533 | 6 |
| `Multiplayer/Lobby/CustomServer/Lobby.CustomServer.xml` | 1508 | 700 | 4 |
| `Native/Information/SceneNotification.xml` | 1500 | 840 | 8 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.xml` | 1350 | 800 | 4 |
| `Multiplayer/Lobby/Popups/Lobby.LevelUpPopup.xml` | 1349 | 221 | 10 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.Badge.Progress.Popup.xml` | 1349 | 200 | 6 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.Rank.Progress.Popup.xml` | 1349 | 200 | 6 |
| `Native/CharacterCreation/CharacterCreationCultureStage.xml` | 1300 | 700 | 6 |
| `SandBox/Clan/Finance/ClanFinance.xml` | 1266 | 74 | 8 |
| `SandBox/Clan/Management/ClanManagement.xml` | 1266 | 74 | 8 |
| `Multiplayer/Lobby/Lobby.Cosmetic.Tier.Background.xml` | 1164 | 964 | 4 |
| `Native/GameMenu/GameMenuTroopSelection.xml` | 1128 | 965 | 6 |
| `SandBox/Inventory/Inventory.xml` | 1080 | 1080 | 4 |
| `SandBox/Map/HeirSelectionPopup.xml` | 1050 | 900 | 4 |
| `SandBox/Map/MarriageOfferPopup.xml` | 1050 | 900 | 4 |
| `Native/Options/SPOptions/OptionItem.xml` | 1000 | 50 | 2 |
| `SandBoxCore/CustomBattle/TroopTypeSelectionPopUp.xml` | 930 | 900 | 6 |
| `SandBox/Barter/BarterScreen.xml` | 912 | 555 | 4 |
| `Native/Order/OrderTransferPopup.xml` | 900 | 370 | 4 |
| `Native/Standard/Standard.TopPanel.xml` | 887 | 150 | 4 |
| `SandBox/GameOver/GameOverScreen.xml` | 887 | 890 | 8 |
| `Multiplayer/MultiplayerReportPlayer.xml` | 860 | 330 | 4 |
| `Multiplayer/Lobby/Lobby.Cosmetic.Obtain.Popup.xml` | 850 | 650 | 12 |
| `SandBox/BoardGame.xml` | 820 | 700 | 6 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Change.Faction.Popup.xml` | 790 | 500 | 2 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Change.Sigil.Popup.xml` | 790 | 500 | 2 |
| `SandBox/QuestsScreen/QuestsScreen.xml` | 755 | 182 | 4 |
| `SandBox/Crafting/NewCraftedWeaponPopup.xml` | 750 | 750 | 12 |
| `SandBox/Map/MapCheats.xml` | 750 | 900 | 4 |
| `Multiplayer/MultiplayerAdminInformation.xml` | 720 | 220 | 8 |
| `Native/Options/SPOptions/Playstation4Binds.xml` | 714 | 445 | 4 |
| `Native/Options/SPOptions/Playstation5Binds.xml` | 714 | 474 | 4 |
| `Native/BannerEditor/BannerEditor.xml` | 694 | 1080 | 4 |
| `Native/CharacterCreation/CharacterCreationClanNamingStage.xml` | 694 | 1080 | 4 |
| `Native/CharacterCreation/CharacterCreationNarrativeStage.xml` | 694 | 1080 | 4 |
| `Native/CharacterCreation/CharacterCreationOptionsStage.xml` | 694 | 1080 | 4 |
| `Native/CharacterCreation/CharacterCreationReviewStage.xml` | 694 | 1080 | 4 |
| `Native/FaceGen/HairOnlyFaceGen.xml` | 694 | 1080 | 4 |
| `SandBox/Education/EducationScreen.xml` | 694 | 1080 | 4 |
| `Native/GameMenu/GameMenuTournamentLeaderboard.xml` | 655 | 555 | 4 |
| `SandBox/CharacterDeveloper/CharacterDeveloper.xml` | 650 | 76 | 6 |
| `SandBoxCore/CustomBattle/CustomBattleScreen.xml` | 650 | 7 | 10 |
| `SandBox/CampaignOptions.xml` | 637 | 809 | 4 |
| `Native/Options/SPOptions/XboxBinds.xml` | 634 | 422 | 4 |
| `SandBox/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml` | 634 | 165 | 6 |
| `SandBox/GatherArmy/CohesionBoostPanel.xml` | 623 | 144 | 8 |
| `SandBox/Mission/Disguise/MissionLosingTarget.xml` | 620 | 18 | 6 |
| `Native/Standard/Standard.Window.xml` | 615 | 114 | 4 |
| `SandBox/Clan/Management/ClanLockedPartyTuple.xml` | 605 | 88 | 2 |
| `Multiplayer/Lobby/Armory/Lobby.Armory.xml` | 600 | 600 | 4 |
| `Multiplayer/Lobby/Lobby.AuthenticationPanel.xml` | 600 | 165 | 4 |
| `Native/Mission/BoundaryCrossing.xml` | 600 | 250 | 2 |
| `SandBox/Clan/ClanMembers.xml` | 600 | 1000 | 12 |
| `SandBox/Clan/ClanPartiesRightPanel.xml` | 600 | 1000 | 8 |
| `SandBox/GatherArmy/ArmyManagement.xml` | 592 | 161 | 6 |
| `SandBox/TownManagement/TownManagement.xml` | 590 | 155 | 6 |
| `Native/Scoreboard/SPScoreboardUnit.xml` | 586 | 35 | 3 |
| `SandBox/Crafting/RefinementCategory.xml` | 585 | 770 | 4 |
| `SandBox/Crafting/SmeltingCategory.xml` | 585 | 800 | 4 |
| `Native/FaceGen/FaceGenHair.xml` | 581 | 14 | 3 |
| `Native/FaceGen/FaceGenTaint.xml` | 581 | 14 | 3 |
| `Native/FaceGen/FaceGenEyes.xml` | 576 | 57 | 5 |
| `Native/FaceGen/FaceGenFace.xml` | 576 | 57 | 5 |
| `Native/FaceGen/FaceGenMouth.xml` | 576 | 57 | 5 |
| `Native/FaceGen/FaceGenNose.xml` | 576 | 57 | 5 |
| `SandBox/Recruitment/RecruitmentPopup.xml` | 576 | 150 | 6 |
| `Native/Options/SPOptions/ExposureOptionsList.xml` | 574 | 742 | 4 |
| `SandBox/Crafting/CraftingCategory.xml` | 573 | 735 | 4 |
| `SandBox/KingdomManagement/Decision/SettlementDecisionPanel.xml` | 560 | 1012 | 4 |
| `SandBox/Mission/MissionQuestBar.xml` | 550 | 12 | 6 |
| `SandBox/Party/PartyScreen.xml` | 550 | 900 | 4 |
| `SandBox/KingdomManagement/Army/ArmiesPanel.xml` | 540 | 923 | 8 |
| `SandBox/KingdomManagement/Clan/ClansPanel.xml` | 540 | 923 | 8 |
| `SandBox/KingdomManagement/Diplomacy/DiplomacyPanel.xml` | 540 | 923 | 8 |
| `SandBox/KingdomManagement/Fiefs/FiefsPanel.xml` | 540 | 923 | 8 |
| `SandBox/KingdomManagement/Policies/PoliciesPanel.xml` | 540 | 923 | 8 |
| `SandBox/CharacterDeveloper/AttributeInspectPopup.xml` | 533 | 627 | 4 |
| `SandBox/MapWait.xml` | 525 | 800 | 2 |
| `Native/Information/Inquiries/MultiSelectionQueryPopup.xml` | 512 | 645 | 4 |
| `Native/Information/Inquiries/TextQueryPopup.xml` | 512 | 645 | 8 |
| `Multiplayer/DCSHelper.xml` | 500 | 550 | 8 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Creation.Popup.xml` | 500 | 275 | 8 |
| `Multiplayer/Lobby/CustomServer/Lobby.CustomServer.HostGame.xml` | 500 | 30 | 6 |
| `Multiplayer/Lobby/Home/Lobby.Home.Sigil.Change.Popup.xml` | 500 | 700 | 8 |
| `Multiplayer/TeamSelection/MultiplayerCultureSelection.xml` | 500 | 50 | 6 |
| `SandBox/QuestsScreen/QuestsStageItem.xml` | 500 | 41 | 6 |
| `Native/SPChatLog.xml` | 495 | 350 | 4 |
| `SandBox/Crafting/CraftingHistoryPopup.xml` | 490 | 630 | 4 |
| `Multiplayer/MPChatLog.xml` | 460 | 254 | 4 |
| `SandBox/SaveLoad/SaveLoadScreen.xml` | 460 | 55 | 8 |
| `Native/Options/SPOptions/KeybindingPopup.xml` | 455 | 205 | 4 |
| `Native/Credits/CreditsScreen.xml` | 450 | 118 | 4 |
| `SandBox/Map/ArmyOverlay.xml` | 450 | 229 | 4 |
| `SandBox/TownManagement/ReservePopup.xml` | 450 | 170 | 3 |
| `SandBoxCore/CustomBattle/ArmyComposition.xml` | 450 | 38 | 4 |
| `Multiplayer/Lobby/Lobby.ClassFilter.xml` | 443 | 660 | 2 |
| `SandBox/KingdomManagement/Decision/KingdomDecision.xml` | 436 | 88 | 4 |
| `SandBox/KingdomManagement/KingdomGiftFiefPopup.xml` | 436 | 88 | 4 |
| `Multiplayer/Lobby/Matchmaking/Lobby.Matchmaking.xml` | 430 | 14 | 6 |
| `Multiplayer/HUD/HUDExtension.xml` | 423 | 75 | 4 |
| `Native/InitialScreen.xml` | 400 | 233 | 4 |
| `Native/PowerLevelComparer.xml` | 400 | 10 | 2 |
| `SandBox/KingdomManagement/Decision/PolicyDecisionPanel.xml` | 400 | 7 | 6 |
| `Native/Mission/ObjectiveTracker/MissionObjectives.xml` | 380 | 14 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaUnitPage.xml` | 370 | 100 | 6 |
| `Native/FaceGen/FaceGenProperty.xml` | 367 | 42 | 3 |
| `Native/Options/SPOptions/ExposureOption.xml` | 367 | 42 | 6 |
| `Native/PhotoModeValueOption.xml` | 367 | 25 | 4 |
| `Multiplayer/Lobby/Armory/Lobby.Armory.ClassStats.xml` | 350 | 187 | 8 |
| `Multiplayer/Lobby/Matchmaking/Lobby.Matchmaking.GameCard.xml` | 350 | 570 | 2 |
| `SandBoxCore/CustomBattle/CompositionSlider.xml` | 350 | 50 | 4 |
| `SandBox/Map/SettlementOverlay.xml` | 348 | 55 | 6 |
| `Multiplayer/HUD/MultiplayerDeathCard.xml` | 346 | 150 | 4 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Creation.Information.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Invite.Friends.Popup.xml` | 342 | 69 | 8 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Leaderboard.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.SendPost.Popup.xml` | 342 | 69 | 8 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.xml` | 342 | 69 | 8 |
| `Multiplayer/Lobby/Lobby.PlayerProfile.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Popups/Lobby.Information.Popup.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.Badge.Selection.Popup.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.BannerlordID.Add.Friend.Popup.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.Rank.Leaderboard.Popup.xml` | 342 | 69 | 6 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.RecentGames.xml` | 342 | 69 | 6 |
| `SandBox/Map/MapSiege/MapSiegeOverlay.xml` | 338 | 42 | 6 |
| `SandBox/Encyclopedia/EncyclopediaBar.xml` | 331 | 94 | 4 |
| `SandBox/Map/EncounterOverlay.xml` | 329 | 201 | 3 |
| `Native/Information/Inquiries/SingleQueryPopup.xml` | 323 | 46 | 6 |
| `Native/Mission/SingleplayerKillfeed.xml` | 306 | 40 | 6 |
| `Multiplayer/AdminPanel/MultiplayerAdminPanel.xml` | 300 | 55 | 8 |
| `Multiplayer/AdminPanel/MultiplayerAdminPanelActionItem.xml` | 300 | 50 | 4 |
| `Multiplayer/AdminPanel/MultiplayerAdminPanelNumericItem.xml` | 300 | 30 | 6 |
| `Multiplayer/AdminPanel/MultiplayerAdminPanelStringItem.xml` | 300 | 30 | 6 |
| `Multiplayer/Lobby/Popups/Lobby.PartyInvitationPopup.xml` | 300 | 10 | 6 |
| `Multiplayer/Lobby/Popups/Lobby.PartyJoinRequestPopup.xml` | 300 | 10 | 8 |
| `Native/OrderOfBattle/OrderOfBattle.Formation.xml` | 300 | 220 | 4 |
| `SandBox/GatherArmy/ArmyManagementRightPanel.xml` | 300 | 50 | 4 |
| `SandBox/Map/MapNotificationItem.xml` | 300 | 113 | 2 |
| `Native/CharacterCreation/CharacterCreationGainedProperties.xml` | 290 | 2 | 3 |
| `Native/Mission/LeaveUI.xml` | 290 | 30 | 6 |
| `SandBox/Education/EducationGainedProperties.xml` | 290 | 2 | 3 |
| `SandBox/Crafting/CraftingPropertyList.xml` | 280 | 17 | 6 |
| `Multiplayer/HUD/MultiplayerKillFeed.xml` | 276 | 45 | 6 |
| `SandBox/Mission/Disguise/MissionMainAgentDetection.xml` | 266 | 32 | 6 |
| `SandBox/Clan/ClanScreen.xml` | 265 | 280 | 4 |
| `SandBox/KingdomManagement/Decision/KingdomDecisionOptionItem.xml` | 255 | 67 | 6 |
| `Native/CharacterCreation/CharacterCreationStageBase.xml` | 251 | 64 | 5 |
| `Native/Options/SPOptions/OptionsGameKeyPage.xml` | 251 | 64 | 6 |
| `Native/Options/SPOptions/OptionsGroupedPage.xml` | 251 | 64 | 6 |
| `Native/Options/SPOptions/BrightnessOption.xml` | 250 | 250 | 8 |
| `Native/TestUI.xml` | 250 | 250 | 3 |
| `Native/Standard/Standard.Button.xml` | 227 | 40 | 2 |
| `Multiplayer/EndOfRound/MultiplayerEndOfRound.xml` | 220 | 220 | 8 |
| `Native/Information/PropertyBasedTooltip.xml` | 220 | 12 | 8 |
| `Native/Mission/AgentFocus.xml` | 220 | 18 | 3 |
| `Native/Mission/MainAgentHUD.xml` | 220 | 18 | 7 |
| `Native/PowerLevelComparerFlat.xml` | 218 | 13 | 6 |
| `Multiplayer/TeamSelection/MultiplayerTeamSelection.xml` | 200 | 100 | 8 |
| `Native/Conversation/SPConversation.xml` | 200 | 80 | 4 |
| `Native/Mission/AgentStatus.xml` | 200 | 80 | 6 |
| `Native/Mission/CheerBark/MainAgentCheerBarkController.xml` | 200 | 200 | 4 |
| `Native/Mission/CheerBark/MainAgentCheerBarkNodeItem.xml` | 200 | 200 | 6 |
| `Native/Mission/MainAgentControlMode.xml` | 200 | 200 | 4 |
| `Native/Mission/MainAgentControllerEquipDrop.xml` | 200 | 200 | 4 |
| `Native/Mission/MainAgentEquipmentController.xml` | 200 | 200 | 4 |
| `SandBox/Clan/ClanPartyRoleDropdown.xml` | 200 | 35 | 4 |
| `StoryMode/TrainingFieldObjectives.xml` | 190 | 160 | 8 |
| `SandBox/Recruitment/RecruitVolunteerTuple.xml` | 180 | 28 | 4 |
| `SandBox/Recruitment/RecruitTroopPanelCart.xml` | 160 | 75 | 4 |
| `SandBox/Inventory/InventoryTooltip.xml` | 155 | 81 | 6 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Invitation.Popup.xml` | 150 | 100 | 10 |
| `Multiplayer/Scoreboard/MultiplayerScoreboardSideTop.xml` | 150 | 82 | 4 |
| `SandBox/Crafting/Crafting.xml` | 140 | 470 | 4 |
| `SandBox/Party/PartyTroopManagerPopUp/PartyTroopRecruitItem.xml` | 135 | 68 | 8 |
| `SandBox/Party/PartyTroopManagerPopUp/PartyTroopUpgradeItem.xml` | 135 | 68 | 8 |
| `Native/Order/OrderTroopItem.xml` | 131 | 223 | 2 |
| `SandBox/Inventory/InventoryEquippedItemSlot.xml` | 130 | 115 | 2 |
| `SandBox/KingdomManagement/Decision/ClanExpelDecisionPanel.xml` | 130 | 100 | 6 |
| `SandBox/Inventory/InventoryItemTuple.xml` | 128 | 60 | 4 |
| `Native/OrderOfBattle/OrderOfBattle.Formation.Marker.xml` | 125 | 55 | 2 |
| `SandBox/Map/GameMenuPartyItem.xml` | 125 | 150 | 2 |
| `Multiplayer/ClassLoadout/MultiplayerClassLoadout.xml` | 124 | 50 | 4 |
| `SandBox/Barter/BarterOfferItemTuple.xml` | 120 | 20 | 14 |
| `SandBox/Nameplate/PartyPlayerNameplateItem.xml` | 120 | 88 | 4 |
| `SandBox/Barter/BarterItemTuple.xml` | 118 | 58 | 8 |
| `Native/Mission/CheerBark/MainAgentCheerBarkNodeCircle.xml` | 117 | 115 | 2 |
| `SandBox/Nameplate/PartyNameplateItem.xml` | 115 | 115 | 4 |
| `SandBox/Clan/Management/ClanLordTuple.xml` | 113 | 84 | 6 |
| `SandBox/Clan/Management/ClanPartyTuple.xml` | 113 | 84 | 6 |
| `SandBox/GatherArmy/GatherArmyTuple.xml` | 112 | 81 | 6 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaUnitTreeNodeItem.xml` | 110 | 80 | 6 |
| `Native/Order/OrderTransferTroopItem.xml` | 109 | 163 | 2 |
| `SandBox/Clan/Finance/ClanExpenseTuple.xml` | 107 | 75 | 4 |
| `SandBox/KingdomManagement/Decision/AcceptCallToWarAgreementDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/Decision/DeclareWarDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/Decision/KingSelectionDecisionPanel.xml` | 105 | 126 | 6 |
| `SandBox/KingdomManagement/Decision/MakePeaceDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/Decision/ProposeCallToWarAgreementDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/Decision/StartAllianceDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/Decision/TradeAgreementDecisionPanel.xml` | 105 | 126 | 8 |
| `SandBox/KingdomManagement/KingdomManagement.xml` | 105 | 126 | 6 |
| `Multiplayer/Lobby/Home/Lobby.Home.xml` | 100 | 100 | 4 |
| `Multiplayer/Lobby/Popups/Lobby.PartyPlayerSuggestionPopup.xml` | 100 | 100 | 10 |
| `Multiplayer/MultiplayerPollingProgress.xml` | 100 | 2 | 8 |
| `Native/BannerBuilder/BannerBuilderBackgroundLayerItem.xml` | 100 | 50 | 2 |
| `Native/BannerBuilder/BannerBuilderSigilLayerItem.xml` | 100 | 50 | 2 |
| `Native/ButtonCancel.xml` | 100 | 80 | 1 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.MatchmakingRequest.Popup.xml` | 99 | 99 | 10 |
| `Multiplayer/ClassLoadout/MultiplayerClassLoadoutItemTab.xml` | 90 | 95 | 4 |
| `Multiplayer/Lobby/Armory/MPLobbyItemTab.xml` | 90 | 95 | 4 |
| `Multiplayer/MultiplayerTeamAvatarsSide.xml` | 90 | 90 | 4 |
| `Native/GameMenu/GameMenuSiege.xml` | 90 | 102 | 8 |
| `Multiplayer/Lobby/Lobby.Rejoin.xml` | 86 | 86 | 8 |
| `Native/FullScreenNotice.xml` | 86 | 86 | 6 |
| `SandBox/Clan/Finance/ClanAlleyTuple.xml` | 84 | 84 | 8 |
| `SandBox/Clan/Finance/ClanIncomeTuple.xml` | 84 | 84 | 8 |
| `SandBox/Clan/Management/ClanSettlementTuple.xml` | 84 | 84 | 8 |
| `Native/FaceGen/FaceGen.xml` | 83 | 516 | 4 |
| `Multiplayer/EndOfRound/MultiplayerEndOfRound.MVP.xml` | 80 | 80 | 8 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.BannerlordID.Change.Popup.xml` | 80 | 80 | 8 |
| `Multiplayer/Scoreboard/MultiplayerScoreboardSideInner.xml` | 80 | 80 | 16 |
| `Native/BannerBuilder/BannerBuilderScreen.xml` | 80 | 80 | 4 |
| `Native/InputDebug.xml` | 80 | 80 | 8 |
| `SandBox/CharacterDeveloper/SkillGridItem.xml` | 80 | 40 | 4 |
| `SandBox/Map/MapEventVisuals.xml` | 80 | 59 | 8 |
| `Native/Mission/SiegeEngineMarker.xml` | 76 | 13 | 7 |
| `Multiplayer/MultiplayerIntermission.xml` | 75 | 75 | 8 |
| `Native/Mission/Crosshair.xml` | 74 | 74 | 4 |
| `SandBox/KingdomManagement/Fiefs/FiefTuple.xml` | 74 | 74 | 8 |
| `Native/Options/SPOptions/Options.xml` | 70 | 70 | 4 |
| `SandBox/Clan/ClanCardSelectionPopup.xml` | 70 | 70 | 6 |
| `SandBox/Clan/Management/ClanVillageItem.xml` | 70 | 70 | 8 |
| `SandBox/Crafting/CraftingHeroPopup.xml` | 70 | 70 | 6 |
| `SandBox/Crafting/CraftingOrderPopup.xml` | 70 | 70 | 6 |
| `SandBox/Crafting/CraftingTemplateSelectionPopup.xml` | 70 | 70 | 6 |
| `Native/Tutorial/TutorialItemPanel.xml` | 68 | 68 | 4 |
| `Multiplayer/ClassLoadout/MultiplayerClassLoadoutUsageItemTab.xml` | 66 | 71 | 4 |
| `Native/Mission/MPMissionMarkerSiegeEngine.xml` | 66 | 66 | 4 |
| `SandBox/KingdomManagement/Clan/ClanTuple.xml` | 66 | 37 | 8 |
| `SandBox/Tournament/Tournament.xml` | 64 | 96 | 8 |
| `Multiplayer/HUD/MultiplayerDuel.xml` | 60 | 70 | 6 |
| `Multiplayer/Lobby/Lobby.Menu.xml` | 60 | 60 | 6 |
| `SandBox/Map/MapNotificationUI.xml` | 60 | 50 | 4 |
| `SandBox/Party/PartyTroopTuple.xml` | 60 | 61 | 6 |
| `SandBox/Party/PartyTroopTupleLeft.xml` | 60 | 61 | 6 |
| `Native/GameMenu/GameMenu.xml` | 58 | 122 | 6 |
| `Native/GameMenu/GameMenuItem.xml` | 56 | 42 | 5 |
| `SandBox/Clan/Management/ClanNotableItem.xml` | 56 | 40 | 6 |
| `Native/Order/Bar/OrderItemBar.xml` | 55 | 55 | 4 |
| `Native/Order/Radial/OrderItemRadial.xml` | 55 | 55 | 4 |
| `Native/GameMenu/GameMenuSiegeMachineItem.xml` | 52 | 52 | 6 |
| `SandBox/Map/MapSiegeMachineButton.xml` | 52 | 52 | 2 |
| `Multiplayer/Lobby/Popups/Lobby.AfterBattlePopup.xml` | 50 | 50 | 4 |
| `Multiplayer/MultiplayerCompass.xml` | 50 | 50 | 6 |
| `Multiplayer/MultiplayerCompassElement.xml` | 50 | 46 | 2 |
| `Multiplayer/TeamSelection/MultiplayerTeamSelectionItem.xml` | 50 | 50 | 6 |
| `Native/Mission/Compass.xml` | 50 | 50 | 6 |
| `Native/Mission/FormationMarker.xml` | 50 | 50 | 10 |
| `Native/OrderOfBattle/OrderOfBattle.xml` | 50 | 50 | 4 |
| `Native/PhotoMode.xml` | 50 | 50 | 4 |
| `SandBox/Clan/Finance/ClanAlleyItem.xml` | 50 | 50 | 8 |
| `SandBox/Clan/Finance/ClanIncomeWorkshop.xml` | 50 | 50 | 8 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaFactionPage.xml` | 50 | 50 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaHeroPage.xml` | 50 | 50 | 10 |
| `SandBox/Inventory/InventoryItemPreview.xml` | 50 | 50 | 3 |
| `Native/Mission/MPDuelTargetMarkers.xml` | 48 | 55 | 10 |
| `Native/Mission/SpectatorControl.xml` | 48 | 48 | 7 |
| `Native/Standard/Standard.DialogCloseButtons.xml` | 48 | 48 | 8 |
| `Native/Mission/MPMissionMarkerFlag.xml` | 47 | 73 | 8 |
| `Native/Mission/MultiplayerServerStatus.xml` | 47 | 48 | 5 |
| `Multiplayer/ClassLoadout/MultiplayerClassLoadoutClassGroup.xml` | 45 | 45 | 10 |
| `Multiplayer/Lobby/Popups/Lobby.Query.Popup.xml` | 45 | 45 | 10 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.RecentGames.Game.xml` | 45 | 60 | 6 |
| `Native/Order/Siege.xml` | 45 | 45 | 8 |
| `SandBox/Clan/ClanFiefs.xml` | 45 | 46 | 10 |
| `SandBox/KingdomManagement/Diplomacy/TruceTuple.xml` | 45 | 45 | 6 |
| `SandBox/KingdomManagement/Diplomacy/WarTuple.xml` | 45 | 45 | 6 |
| `SandBox/Mission/Disguise/MissionStealthFailCounter.xml` | 45 | 21 | 12 |
| `SandBox/Tournament/Tournament.Participant.xml` | 45 | 30 | 8 |
| `Native/FaceGen/FaceGenColorSelector.xml` | 44 | 44 | 6 |
| `SandBox/Encyclopedia/EncyclopediaList/EncyclopediaFilterListItem.xml` | 44 | 44 | 3 |
| `Native/EscapeMenu.xml` | 43 | 56 | 8 |
| `Native/Standard/Standard.TripleDialogCloseButtons.xml` | 41 | 36 | 6 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaQuickNavigation.xml` | 41 | 48 | 10 |
| `Multiplayer/AdminPanel/MultiplayerAdminPanelToggleItem.xml` | 40 | 40 | 8 |
| `Multiplayer/ClassLoadout/MultiplayerClassLoadoutPerkPopup.xml` | 40 | 40 | 12 |
| `Multiplayer/Lobby/Armory/Lobby.Armory.Cosmetics.xml` | 40 | 40 | 6 |
| `Multiplayer/Lobby/Armory/MPLobbyPerkPopup.xml` | 40 | 40 | 12 |
| `Multiplayer/Lobby/FriendsPanel/Lobby.Friends.xml` | 40 | 40 | 8 |
| `Multiplayer/Lobby/Profile/Lobby.Profile.RecentGames.Game.Team.xml` | 40 | 40 | 6 |
| `Native/Mission/MPMissionMarkers.xml` | 40 | 40 | 8 |
| `Native/Mission/ObjectiveTracker/MissionObjectiveMarkers.xml` | 40 | 40 | 6 |
| `Native/Scoreboard/SPScoreboardSkillItem.xml` | 40 | 40 | 3 |
| `Native/Standard/Standard.DropdownWithHorizontalControl.xml` | 40 | 43 | 4 |
| `Native/Standard/Standard.PopupBackButton.xml` | 40 | 40 | 8 |
| `Native/Standard/Standard.PopupCloseButton.xml` | 40 | 40 | 10 |
| `SandBox/Clan/Management/ProfitStatItem.xml` | 40 | 40 | 8 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaSettlementPageStats.xml` | 40 | 40 | 8 |
| `SandBox/GatherArmy/ArmyCartItem.xml` | 40 | 40 | 4 |
| `Native/GameMenu/GameMenuSiegeWallItem.xml` | 39 | 88 | 2 |
| `SandBox/Mission/Disguise/MissionDetectionMarkers.Alternative.Marker.Item.xml` | 38 | 48 | 4 |
| `SandBox/Mission/Stealth/AgentAlarmStateMissionView.xml` | 38 | 45 | 3 |
| `Native/Standard/Standard.TriplePopupCloseButtons.xml` | 37 | 32 | 6 |
| `Native/Scoreboard/SPScoreboardSideHeader.xml` | 36 | 29 | 8 |
| `SandBox/Barter/BarterItemVisual.xml` | 36 | 36 | 6 |
| `SandBox/Map/MapBar.xml` | 36 | 36 | 4 |
| `Multiplayer/HUD/MultiplayerSpectatorHUD.xml` | 35 | 35 | 8 |
| `Native/Scoreboard/SPScoreboardShipsGrid.xml` | 35 | 35 | 10 |
| `SandBox/Clan/ClanParties.xml` | 35 | 35 | 12 |
| `SandBox/Crafting/CraftingPieceGrid.xml` | 35 | 35 | 8 |
| `SandBox/Encyclopedia/EncyclopediaList/EncyclopediaItemListItem.xml` | 35 | 35 | 5 |
| `SandBox/Inventory/InventoryEquippedItemControls.xml` | 35 | 35 | 6 |
| `SandBox/Inventory/InventoryTooltipInnerContent.xml` | 35 | 35 | 6 |
| `Multiplayer/HUD/MultiplayerDuelKillFeed.xml` | 34 | 38 | 10 |
| `Native/Conversation/ConversationItem.xml` | 33 | 32 | 8 |
| `Native/GamepadCursor.xml` | 32 | 32 | 4 |
| `Native/Scoreboard/SPScoreboard.xml` | 32 | 32 | 6 |
| `Native/Order/Bar/OrderSetBar.xml` | 30 | 41 | 4 |
| `SandBox/TownManagement/DevelopmentItem.xml` | 30 | 30 | 4 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Overview.xml` | 29 | 37 | 8 |
| `Multiplayer/Lobby/FriendsPanel/Lobby.FriendItem.xml` | 29 | 46 | 4 |
| `SandBox/KingdomManagement/Army/ArmyTuple.xml` | 29 | 29 | 4 |
| `SandBox/KingdomManagement/Diplomacy/DiplomacyWarLogElement.xml` | 29 | 37 | 5 |
| `Multiplayer/HUD/HUDExtensionFlag.xml` | 28 | 41 | 4 |
| `SandBox/Nameplate/SettlementNameplateItemLarge.xml` | 26 | 20 | 8 |
| `SandBox/Nameplate/SettlementNameplateItemMedium.xml` | 26 | 20 | 8 |
| `SandBox/Nameplate/SettlementNameplateItemSmall.xml` | 26 | 20 | 8 |
| `Multiplayer/HUD/MultiplayerVoiceChat.xml` | 25 | 25 | 8 |
| `Native/Mission/NameMarker.xml` | 25 | 42 | 11 |
| `Multiplayer/Lobby/Clan/Lobby.Clan.Roster.xml` | 23 | 23 | 10 |
| `Native/Standard/Standard.VerticalScrollbar.xml` | 22 | 800 | 2 |
| `SandBox/Map/MapTrackers.xml` | 22 | 22 | 12 |
| `SandBox/Party/PartySortController.xml` | 22 | 20 | 4 |
| `Multiplayer/Lobby/FriendsPanel/Lobby.Friends.ServiceTabItem.xml` | 21 | 21 | 6 |
| `Native/Mission/AgentLockTargets.xml` | 20 | 20 | 6 |
| `Native/Standard/Standard.Slider.Float.xml` | 20 | 28 | 4 |
| `SandBox/Clan/ClanIncome.xml` | 20 | 20 | 14 |
| `SandBox/Clan/ClanPartiesLeftPanel.xml` | 20 | 20 | 10 |
| `SandBox/Encyclopedia/EncyclopediaList/EncyclopediaItemList.xml` | 20 | 20 | 10 |
| `SandBox/GatherArmy/ArmyManagementLeftPanel.xml` | 20 | 20 | 10 |
| `SandBox/Inventory/InventoryList.xml` | 20 | 20 | 8 |
| `SandBox/Mission/Disguise/MissionDetectionMarkers.xml` | 20 | 20 | 4 |
| `SandBox/Recruitment/RecruitTroopPanel.xml` | 20 | 20 | 6 |
| `Native/Options/SPOptions/OptionsGamepadPage.xml` | 19 | 19 | 18 |
| `SandBox/Encyclopedia/EncyclopediaList/EncyclopediaFilterGroup.xml` | 19 | 19 | 7 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaDivider.xml` | 19 | 19 | 6 |
| `SandBox/Map/MapSiege.xml` | 19 | 608 | 4 |
| `Multiplayer/Lobby/FriendsPanel/Lobby.FriendGroup.xml` | 17 | 20 | 6 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaSubPageElement.xml` | 16 | 14 | 4 |
| `StoryMode/TrainingFieldObjectiveItem.xml` | 15 | 15 | 6 |
| `Native/Order/Radial/OrderSetRadial.xml` | 13 | 13 | 4 |
| `Multiplayer/MultiplayerPollingInitiation.xml` | 12 | 315 | 8 |
| `Native/Standard/Standard.CircleLoadingWidget.xml` | 12 | 12 | 6 |
| `Multiplayer/Lobby/Home/Lobby.Home.Announcements.Panel.xml` | 8 | 10 | 10 |
| `Native/FaceGen/FaceGenBody.xml` | 8 | 10 | 5 |
| `Native/FaceGen/FaceGenGrid.xml` | 8 | 10 | 6 |
| `Native/Options/MPOptions/MPOptions.xml` | 8 | 10 | 8 |
| `SandBox/Clan/Finance/ClanSupporters.xml` | 8 | 10 | 8 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaClanPage.xml` | 8 | 50 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaConceptPage.xml` | 8 | 50 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaSettlementPage.xml` | 8 | 50 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaShipPage.xml` | 8 | 50 | 10 |
| `SandBox/Map/MapIncident/MapIncident.xml` | 8 | 10 | 22 |
| `SandBox/TownManagement/GovernorSelectionPopup.xml` | 8 | 10 | 10 |
| `SandBox/Encyclopedia/EncyclopediaSubPages/EncyclopediaSubPageHistoryElement.xml` | 6 | 6 | 3 |
| `Native/Options/MPOptions/MPOptionsTabToggle.xml` | 4 | 60 | 4 |
| `SandBox/Map/OverlayPopup.xml` | 1 | 1 | 4 |

## 7. 参考：标准控件贴图尺寸（StdAssets）

> 贴图像素尺寸不等同 UI 显示尺寸；九宫格 sprite 会被拉伸。此处仅供了解标准控件天生比例。

| Sprite | 宽 | 高 |
|---|---|---|
| `StdAssets\Popup\canvas` | 512 | 645 |
| `StdAssets\Popup\canvas_dark` | 699 | 666 |
| `StdAssets\triple_button_frame` | 713 | 126 |
| `StdAssets\triple_button_left` | 296 | 55 |
| `StdAssets\triple_button_mid` | 96 | 82 |
| `StdAssets\triple_button_right` | 293 | 55 |
