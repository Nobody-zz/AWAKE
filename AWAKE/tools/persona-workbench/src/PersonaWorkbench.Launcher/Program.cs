using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("Persona Workbench Launcher")]
[assembly: AssemblyProduct("Persona Workbench")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace PersonaWorkbench.Launcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string integrityError;
            bool packageValid = PackageIntegrity.TryValidate(root, out integrityError);
            if (args.Length == 1 && args[0] == "--smoke-test") return packageValid ? 0 : 2;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!packageValid)
            {
                MessageBox.Show(
                    "发布包文件不完整或混入了其他版本。请删除当前目录，重新把完整 ZIP 解压到一个空目录。\r\n\r\n" + integrityError,
                    "Persona Workbench 包校验失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return 3;
            }
            Application.Run(new LauncherForm());
            return 0;
        }
    }

    internal static class PackageIntegrity
    {
        internal static bool TryValidate(string root, out string error)
        {
            try
            {
                string manifestPath = Path.Combine(root, "PACKAGE-MANIFEST.sha256.txt");
                if (!File.Exists(manifestPath))
                {
                    error = "缺少 PACKAGE-MANIFEST.sha256.txt。";
                    return false;
                }

                string rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                foreach (string originalLine in File.ReadAllLines(manifestPath, Encoding.UTF8))
                {
                    string line = originalLine.TrimStart('\uFEFF');
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int separator = line.IndexOf("  ", StringComparison.Ordinal);
                    if (separator != 64)
                    {
                        error = "Manifest 格式无效。";
                        return false;
                    }

                    string expectedHash = line.Substring(0, separator);
                    string relativePath = line.Substring(separator + 2);
                    string filePath = Path.GetFullPath(Path.Combine(root, relativePath));
                    if (!filePath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
                    {
                        error = "缺少或越界文件：" + relativePath;
                        return false;
                    }

                    string actualHash = ComputeHash(filePath);
                    if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        error = "文件版本或内容不匹配：" + relativePath;
                        return false;
                    }
                }

                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string ComputeHash(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                byte[] hash = sha256.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("X2"));
                return text.ToString();
            }
        }
    }

    internal sealed class LauncherForm : Form
    {
        private const string Url = "http://127.0.0.1:51337/";
        private readonly string root = AppDomain.CurrentDomain.BaseDirectory;
        private readonly Label status = new Label();
        private readonly Button launch = new Button();
        private readonly Button open = new Button();
        private readonly Button stop = new Button();

        internal LauncherForm()
        {
            Text = "Persona Workbench 启动器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(620, 380);
            BackColor = Color.FromArgb(246, 248, 252);
            Font = new Font("Microsoft YaHei UI", 9F);
            Icon = SystemIcons.Application;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 108;
            header.BackColor = Color.FromArgb(38, 74, 145);
            AddHeaderLabel(header, "Persona Workbench", 20, 20F, FontStyle.Bold, Color.White);
            AddHeaderLabel(header, "独立人格工作台 · 不启动 Bannerlord · 不自动调用云端 AI", 68, 10F, FontStyle.Regular, Color.FromArgb(221, 230, 248));
            Controls.Add(header);

            Controls.Add(new Label { AutoSize = true, Location = new Point(34, 134), Text = "当前状态" });
            status.Location = new Point(112, 130);
            status.Size = new Size(474, 26);
            status.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            status.Text = "正在检查本机服务……";
            Controls.Add(status);

            launch.Name = "LaunchWorkbenchButton";
            launch.Location = new Point(34, 168);
            launch.Size = new Size(552, 70);
            launch.FlatStyle = FlatStyle.Flat;
            launch.FlatAppearance.BorderSize = 0;
            launch.BackColor = Color.FromArgb(31, 137, 88);
            launch.ForeColor = Color.White;
            launch.Font = new Font("Microsoft YaHei UI", 14F, FontStyle.Bold);
            launch.Text = "启动并打开 Persona Workbench";
            launch.Cursor = Cursors.Hand;
            launch.Click += delegate { StartWorkbench(); };
            Controls.Add(launch);

            open = AddButton("仅打开页面", 34, delegate { OpenUrl(); });
            stop = AddButton("停止后台服务", 174, delegate { RunScript("stop-free-preview.ps1", "正在停止服务……"); });
            AddButton("打开程序目录", 314, delegate { Process.Start("explorer.exe", "\"" + root + "\""); });
            AddButton("关闭启动器", 454, delegate { Close(); });

            Controls.Add(new Label {
                Location = new Point(34, 318), Size = new Size(552, 42), ForeColor = Color.FromArgb(93, 101, 117),
                Text = "关闭启动器不会关闭工作台。彻底停止请点击“停止后台服务”。"
            });
            Shown += delegate { CheckStatus(); };
        }

        private void AddHeaderLabel(Control parent, string text, int top, float size, FontStyle style, Color color)
        {
            parent.Controls.Add(new Label {
                AutoSize = true, Location = new Point(30, top), ForeColor = color,
                Font = new Font("Microsoft YaHei UI", size, style), Text = text
            });
        }

        private Button AddButton(string text, int left, EventHandler action)
        {
            Button button = new Button { Location = new Point(left, 257), Size = new Size(132, 42), Text = text };
            button.Click += action;
            Controls.Add(button);
            return button;
        }

        private void StartWorkbench()
        {
            if (Ready())
            {
                OpenUrl();
                SetState(true, "服务已运行，已打开工作台");
                return;
            }
            RunScript("start-free-preview.ps1", "正在启动本机服务……");
        }

        private void RunScript(string name, string busyText)
        {
            string scriptPath = Path.Combine(root, name);
            if (!File.Exists(scriptPath))
            {
                SetState(false, "缺少文件：" + name);
                return;
            }

            SetBusy(busyText);
            ThreadPool.QueueUserWorkItem(delegate
            {
                int exitCode;
                string error;
                RunPowerShell(scriptPath, out exitCode, out error);
                bool ready = Ready();
                if (IsDisposed) return;
                BeginInvoke(new Action(delegate
                {
                    bool starting = name.StartsWith("start", StringComparison.OrdinalIgnoreCase);
                    if (starting && exitCode == 0 && ready)
                        SetState(true, "启动成功，工作台窗口已打开");
                    else if (!starting && exitCode == 0 && !ready)
                        SetState(false, "后台服务已停止");
                    else
                    {
                        PersistFailureLog(error);
                        string message = string.IsNullOrWhiteSpace(error) ? "操作失败，请查看 .runtime 日志" : FirstLine(error);
                        SetState(ready, message);
                        MessageBox.Show(this, message, "Persona Workbench", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
        }

        private void RunPowerShell(string scriptPath, out int exitCode, out string error)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                info.FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
                info.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + scriptPath + "\"";
                info.WorkingDirectory = root;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = false;
                info.RedirectStandardError = false;
                using (Process process = Process.Start(info))
                {
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    error = exitCode == 0 ? string.Empty : ReadFailureLog();
                }
            }
            catch (Exception exception)
            {
                exitCode = -1;
                error = exception.Message;
            }
        }

        private string ReadFailureLog()
        {
            foreach (string fileName in new[] { "startup.log", "server.stderr.log" })
            {
                try
                {
                    string logPath = Path.Combine(root, ".runtime", fileName);
                    if (!File.Exists(logPath)) continue;
                    string text = File.ReadAllText(logPath, Encoding.UTF8).Trim();
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                catch { }
            }
            return "PowerShell 启动失败，但未生成可读日志。请重新完整解压发布包，并检查安全软件。";
        }

        private void PersistFailureLog(string error)
        {
            try
            {
                string runtime = Path.Combine(root, ".runtime");
                Directory.CreateDirectory(runtime);
                string errorLogPath = Path.Combine(runtime, "server.stderr.log");
                string startupLogPath = Path.Combine(runtime, "startup.log");
                string detail = string.IsNullOrWhiteSpace(error) ? "Launcher operation failed without stderr." : error.Trim();
                string entry = "[" + DateTimeOffset.Now.ToString("O") + "] launcher_error" + Environment.NewLine + detail + Environment.NewLine;
                if (!File.Exists(startupLogPath) || new FileInfo(startupLogPath).Length == 0) File.WriteAllText(startupLogPath, entry, new UTF8Encoding(false));
                if (!File.Exists(errorLogPath) || new FileInfo(errorLogPath).Length == 0) File.WriteAllText(errorLogPath, entry, new UTF8Encoding(false));
            }
            catch { }
        }

        private void CheckStatus()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool ready = Ready();
                if (IsDisposed) return;
                BeginInvoke(new Action(delegate { SetState(ready, ready ? "服务已运行，可以直接打开" : "服务未启动"); }));
            });
        }

        private bool Ready()
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
                request.Timeout = 900;
                request.Proxy = null;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    return response.StatusCode == HttpStatusCode.OK;
            }
            catch { return false; }
        }

        private void OpenUrl()
        {
            try { Process.Start(new ProcessStartInfo { FileName = Url, UseShellExecute = true }); }
            catch (Exception exception) { MessageBox.Show(this, exception.Message, "无法打开页面"); }
        }

        private void SetBusy(string text)
        {
            status.ForeColor = Color.FromArgb(173, 105, 24);
            status.Text = text;
            launch.Enabled = open.Enabled = stop.Enabled = false;
        }

        private void SetState(bool ready, string text)
        {
            status.ForeColor = ready ? Color.FromArgb(29, 125, 78) : Color.FromArgb(105, 112, 125);
            status.Text = text;
            launch.Text = ready ? "打开 Persona Workbench" : "启动并打开 Persona Workbench";
            launch.Enabled = true;
            open.Enabled = stop.Enabled = ready;
        }

        private static string FirstLine(string text)
        {
            using (StringReader reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            }
            return "操作失败";
        }
    }
}
