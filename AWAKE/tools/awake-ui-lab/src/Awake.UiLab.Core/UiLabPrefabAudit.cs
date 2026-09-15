using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Awake.UiLab
{
    /// <summary>从 Prefab XML 抽出的绑定契约。</summary>
    public sealed class UiLabPrefabContract
    {
        public IReadOnlyList<string> Bindings { get; private set; }
        public IReadOnlyList<string> Commands { get; private set; }
        public IReadOnlyList<string> NodeIds { get; private set; }

        internal UiLabPrefabContract(List<string> bindings, List<string> commands, List<string> nodeIds)
        {
            Bindings = bindings;
            Commands = commands;
            NodeIds = nodeIds;
        }
    }

    /// <summary>
    /// 基线 Prefab 的静态审计：把 XML 里声明的绑定与命令抽出来，
    /// 与夹具 DTO 提供的绑定/命令做一致性比对，防止 Prefab 与本地夹具漂移。
    /// 纯文本解析，不加载 Gauntlet，也不启动游戏。
    /// </summary>
    public static class UiLabPrefabAudit
    {
        private static readonly Regex BindingAt = new Regex(@"@([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        private static readonly Regex BindingBrace = new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
        private static readonly Regex CommandAttribute = new Regex(
            @"Command\.[A-Za-z_][A-Za-z0-9_]*\s*=\s*""([A-Za-z_][A-Za-z0-9_]*)""", RegexOptions.Compiled);
        private static readonly Regex NodeIdAttribute = new Regex(
            @"\bId\s*=\s*""([^""]+)""", RegexOptions.Compiled);

        public static UiLabPrefabContract Audit(string prefabXml)
        {
            if (prefabXml == null) throw new ArgumentNullException("prefabXml");

            List<string> bindings = new List<string>();
            List<string> commands = new List<string>();
            List<string> nodeIds = new List<string>();

            foreach (Match match in BindingAt.Matches(prefabXml))
            {
                AddDistinct(bindings, match.Groups[1].Value);
            }
            foreach (Match match in BindingBrace.Matches(prefabXml))
            {
                AddDistinct(bindings, match.Groups[1].Value);
            }
            foreach (Match match in CommandAttribute.Matches(prefabXml))
            {
                AddDistinct(commands, match.Groups[1].Value);
            }
            foreach (Match match in NodeIdAttribute.Matches(prefabXml))
            {
                AddDistinct(nodeIds, match.Groups[1].Value);
            }

            return new UiLabPrefabContract(bindings, commands, nodeIds);
        }

        /// <summary>Prefab 声明的每个绑定/命令都必须能被夹具 DTO 提供；返回未覆盖的悬空项。</summary>
        public static IReadOnlyList<string> FindDanglingPrefabMembers(UiLabPrefabContract contract)
        {
            if (contract == null) throw new ArgumentNullException("contract");

            List<string> dangling = new List<string>();
            for (int i = 0; i < contract.Bindings.Count; i++)
            {
                if (!Contains(UiLabFixtureView.BindingNames, contract.Bindings[i]))
                {
                    dangling.Add("binding:" + contract.Bindings[i]);
                }
            }
            for (int i = 0; i < contract.Commands.Count; i++)
            {
                if (!Contains(UiLabFixtureView.CommandNames, contract.Commands[i]))
                {
                    dangling.Add("command:" + contract.Commands[i]);
                }
            }
            return dangling;
        }

        /// <summary>夹具 DTO 是否覆盖了指定状态 ID 所需的确认/拒绝命令。</summary>
        public static bool SupportsDecisionCommands()
        {
            return Contains(UiLabFixtureView.CommandNames, "ExecuteConfirm")
                && Contains(UiLabFixtureView.CommandNames, "ExecuteReject");
        }

        private static void AddDistinct(List<string> target, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (int i = 0; i < target.Count; i++)
            {
                if (string.Equals(target[i], value, StringComparison.Ordinal)) return;
            }
            target.Add(value);
        }

        internal static bool Contains(string[] source, string value)
        {
            for (int i = 0; i < source.Length; i++)
            {
                if (string.Equals(source[i], value, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
