using System;
using System.Collections.Generic;

namespace Awake;

internal enum PermissionCategory
{
    PlayerKnown,
    Route,
    Prompt,
    Storage,
    Rag,
    Command,
    CloudExport
}

internal enum PermissionEnforcement
{
    Hard,
    Soft
}

internal sealed class PermissionDefinition
{
    internal string Id { get; }
    internal PermissionCategory Category { get; }
    internal PermissionEnforcement Enforcement { get; }
    internal string Purpose { get; }
    internal string DisplayName { get; }

    internal PermissionDefinition(
        string id,
        PermissionCategory category,
        PermissionEnforcement enforcement,
        string purpose,
        string displayName)
    {
        Id = id ?? string.Empty;
        Category = category;
        Enforcement = enforcement;
        Purpose = purpose ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
    }

    internal bool CanDegrade => Enforcement == PermissionEnforcement.Soft;
}

internal static class PermissionCatalog
{
    internal static PermissionDefinition[] All { get; } = BuildAll();
    internal static string[] ManifestPermissionIds { get; } = BuildManifestIds();

    internal static bool TryGet(string permissionId, out PermissionDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(permissionId)) return false;
        foreach (PermissionDefinition candidate in All)
        {
            if (StringComparer.Ordinal.Equals(candidate.Id, permissionId))
            {
                definition = candidate;
                return true;
            }
        }
        return false;
    }

    internal static PermissionDefinition RoutePermission(string routeId)
    {
        string id = AiTaskConstants.RoutePermission(routeId);
        PermissionDefinition definition;
        if (TryGet(id, out definition)) return definition;
        return new PermissionDefinition(
            id,
            PermissionCategory.Route,
            PermissionEnforcement.Hard,
            "调用 AI 路由：" + routeId + "。",
            "AI 路由 " + routeId);
    }

    internal static PermissionDefinition CommandPermission(string commandId)
    {
        string id = AiTaskConstants.CommandPermission(commandId);
        PermissionDefinition definition;
        if (TryGet(id, out definition)) return definition;
        return new PermissionDefinition(
            id,
            PermissionCategory.Command,
            PermissionEnforcement.Hard,
            "执行世界状态命令：" + commandId + "。",
            "世界状态命令 " + commandId);
    }

    internal static PermissionDefinition CloudExportPermission(string classification)
    {
        string id = CloudExportPermissionId(classification);
        PermissionDefinition definition;
        if (TryGet(id, out definition)) return definition;
        return new PermissionDefinition(
            id,
            PermissionCategory.CloudExport,
            PermissionEnforcement.Hard,
            "将分类 " + classification + " 的数据外发到云 AI Provider。",
            "云外发分类 " + classification);
    }

    internal static string CloudExportPermissionId(string classification)
    {
        return "ai.cloud_export:" + classification;
    }

    private static PermissionDefinition[] BuildAll()
    {
        List<PermissionDefinition> all = new List<PermissionDefinition>
        {
            new PermissionDefinition(
                AwakeConstants.PermissionPlayerKnownRead,
                PermissionCategory.PlayerKnown,
                PermissionEnforcement.Soft,
                "读取当前玩家信息以构建 AI 上下文。",
                "玩家情报读取"),
            new PermissionDefinition(
                NpcDialogueConstants.PermissionRouteInvoke,
                PermissionCategory.Route,
                PermissionEnforcement.Hard,
                "调用 NPC 对话 AI 路由完成交谈。",
                "NPC 对话路由"),
            new PermissionDefinition(
                AwakeConstants.PermissionPromptRegistryWrite,
                PermissionCategory.Prompt,
                PermissionEnforcement.Soft,
                "登记运行时提示词到框架提示词注册表。",
                "提示词登记"),
            new PermissionDefinition(
                NpcDialogueConstants.PermissionPromptCompile,
                PermissionCategory.Prompt,
                PermissionEnforcement.Soft,
                "编译 NPC 对话提示词并注入变量。",
                "NPC 提示词编译"),
            new PermissionDefinition(
                AwakeConstants.PermissionStorageWrite,
                PermissionCategory.Storage,
                PermissionEnforcement.Soft,
                "写入运行时记忆与事件状态命名空间。",
                "运行时存储写入"),
            new PermissionDefinition(
                KnowledgeConstants.PermissionRagWrite,
                PermissionCategory.Rag,
                PermissionEnforcement.Soft,
                "写入世界知识语料到 RAG 集合。",
                "世界知识写入"),
            new PermissionDefinition(
                KnowledgeConstants.PermissionRagRead,
                PermissionCategory.Rag,
                PermissionEnforcement.Soft,
                "检索世界知识语料。",
                "世界知识检索"),
            new PermissionDefinition(
                CloudExportPermissionId(CloudExportPolicy.PlayerState),
                PermissionCategory.CloudExport,
                PermissionEnforcement.Hard,
                "将玩家状态与角色上下文外发到云 AI Provider。",
                "云外发·玩家状态")
        };

        foreach (string routeId in AiTaskConstants.NewRouteIds)
        {
            all.Add(new PermissionDefinition(
                AiTaskConstants.RoutePermission(routeId),
                PermissionCategory.Route,
                PermissionEnforcement.Hard,
                "调用 AI 路由：" + routeId + "。",
                "AI 路由 " + routeId));
        }

        foreach (string commandId in AiTaskConstants.NewCommandIds)
        {
            all.Add(new PermissionDefinition(
                AiTaskConstants.CommandPermission(commandId),
                PermissionCategory.Command,
                PermissionEnforcement.Hard,
                "执行世界状态命令：" + commandId + "。",
                "世界状态命令 " + commandId));
        }

        return all.ToArray();
    }

    private static string[] BuildManifestIds()
    {
        string[] ids = new string[All.Length];
        for (int i = 0; i < All.Length; i++)
        {
            ids[i] = All[i].Id;
        }
        return ids;
    }
}
