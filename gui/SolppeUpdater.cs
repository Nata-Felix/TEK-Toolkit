using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("SOLPPE_updater")]
[assembly: AssemblyProduct("SOLPPE_toolkit")]
[assembly: AssemblyCompany("SOLPPE")]
[assembly: AssemblyCopyright("Copyright Natã 2026")]
[assembly: AssemblyVersion("1.0.5.0")]
[assembly: AssemblyFileVersion("1.0.5.0")]

namespace SolppeUpdater
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (args.Length == 1 && String.Equals(args[0], "--self-test", StringComparison.OrdinalIgnoreCase))
            {
                return UpdaterEngine.RunSelfTest() ? 0 : 1;
            }

            UpdateOptions options;
            string error;
            if (!UpdateOptions.TryParse(args, out options, out error))
            {
                MessageBox.Show(error, "SOLPPE | Atualizador", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new UpdaterForm(options));
            return 0;
        }
    }

    internal sealed class UpdateOptions
    {
        public int ParentProcessId;
        public string TargetPath;
        public string DownloadUrl;
        public string Version;
        public string Sha256;

        public static bool TryParse(string[] args, out UpdateOptions options, out string error)
        {
            options = null;
            error = null;

            try
            {
                UpdateOptions parsed = new UpdateOptions();
                for (int i = 0; i < args.Length; i++)
                {
                    string key = args[i];
                    if (i + 1 >= args.Length)
                    {
                        error = "Parametro sem valor: " + key;
                        return false;
                    }

                    string value = args[++i];
                    if (String.Equals(key, "--pid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Int32.TryParse(value, out parsed.ParentProcessId) || parsed.ParentProcessId <= 0)
                        {
                            error = "PID do toolkit invalido.";
                            return false;
                        }
                    }
                    else if (String.Equals(key, "--target", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.TargetPath = Path.GetFullPath(value);
                    }
                    else if (String.Equals(key, "--url", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.DownloadUrl = value;
                    }
                    else if (String.Equals(key, "--version", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.Version = value;
                    }
                    else if (String.Equals(key, "--sha256", StringComparison.OrdinalIgnoreCase))
                    {
                        parsed.Sha256 = value;
                    }
                    else
                    {
                        error = "Parametro desconhecido: " + key;
                        return false;
                    }
                }

                if (parsed.ParentProcessId <= 0 || String.IsNullOrWhiteSpace(parsed.TargetPath) || String.IsNullOrWhiteSpace(parsed.DownloadUrl))
                {
                    error = "Os dados da atualizacao estao incompletos.";
                    return false;
                }

                if (!String.Equals(Path.GetFileName(parsed.TargetPath), "SOLPPE_toolkit.exe", StringComparison.OrdinalIgnoreCase))
                {
                    error = "O atualizador somente pode substituir SOLPPE_toolkit.exe.";
                    return false;
                }

                Uri uri;
                if (!Uri.TryCreate(parsed.DownloadUrl, UriKind.Absolute, out uri) || !UpdaterEngine.IsAllowedGitHubUri(uri))
                {
                    error = "A origem do novo toolkit nao e uma URL HTTPS valida do GitHub.";
                    return false;
                }

                parsed.Sha256 = UpdaterEngine.NormalizeSha256(parsed.Sha256);
                if (String.IsNullOrWhiteSpace(parsed.Sha256))
                {
                    error = "O SHA-256 do novo toolkit nao foi informado pela release.";
                    return false;
                }
                parsed.Version = String.IsNullOrWhiteSpace(parsed.Version) ? "nova versao" : parsed.Version.Trim();
                options = parsed;
                return true;
            }
            catch (Exception ex)
            {
                error = "Parametros de atualizacao invalidos: " + ex.Message;
                return false;
            }
        }
    }

    internal sealed class UpdaterForm : Form
    {
        private readonly UpdateOptions options;
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color emerald = Color.FromArgb(8, 154, 103);
        private readonly Label statusLabel = new Label();
        private readonly Label detailLabel = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Button closeButton = new Button();

        public UpdaterForm(UpdateOptions options)
        {
            this.options = options;
            Text = "SOLPPE | Atualizador";
            ClientSize = new Size(560, 225);
            MinimumSize = MaximumSize = Size;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F);

            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ToolkitAll.Assets.SolppeHandshake.ico"))
                {
                    if (stream != null)
                    {
                        Icon = new Icon(stream);
                    }
                }
            }
            catch
            {
            }

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 64;
            header.BackColor = darkBlue;
            Controls.Add(header);

            Label title = new Label();
            title.Text = "SOLPPE_toolkit";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
            title.AutoSize = true;
            title.Left = 22;
            title.Top = 17;
            header.Controls.Add(title);

            Label version = new Label();
            version.Text = "Atualizando para " + options.Version;
            version.ForeColor = Color.FromArgb(202, 230, 226);
            version.AutoSize = true;
            version.Left = 336;
            version.Top = 23;
            header.Controls.Add(version);

            statusLabel.Text = "Preparando atualizacao...";
            statusLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
            statusLabel.ForeColor = darkBlue;
            statusLabel.Left = 24;
            statusLabel.Top = 85;
            statusLabel.Width = 510;
            statusLabel.Height = 26;
            Controls.Add(statusLabel);

            progressBar.Left = 24;
            progressBar.Top = 119;
            progressBar.Width = 510;
            progressBar.Height = 22;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            Controls.Add(progressBar);

            detailLabel.Text = "O aplicativo sera reaberto automaticamente.";
            detailLabel.ForeColor = Color.FromArgb(89, 111, 125);
            detailLabel.Left = 24;
            detailLabel.Top = 151;
            detailLabel.Width = 400;
            detailLabel.Height = 42;
            Controls.Add(detailLabel);

            closeButton.Text = "Fechar";
            closeButton.Left = 438;
            closeButton.Top = 163;
            closeButton.Width = 96;
            closeButton.Height = 34;
            closeButton.BackColor = emerald;
            closeButton.ForeColor = Color.White;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Enabled = false;
            closeButton.Click += delegate { Close(); };
            Controls.Add(closeButton);

            Shown += delegate
            {
                ThreadPool.QueueUserWorkItem(delegate { RunUpdate(); });
            };
        }

        private void RunUpdate()
        {
            string downloadPath = null;

            try
            {
                SetProgress(5, "Encerrando SOLPPE_toolkit...", "Finalizando o processo anterior com seguranca.");
                UpdaterEngine.StopProcess(options.ParentProcessId, options.TargetPath);

                string directory = Path.GetDirectoryName(options.TargetPath);
                if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    throw new DirectoryNotFoundException("A pasta do toolkit nao foi encontrada.");
                }

                downloadPath = Path.Combine(directory, ".SOLPPE_toolkit.update." + Guid.NewGuid().ToString("N") + ".tmp");
                SetProgress(15, "Baixando " + options.Version + "...", "Recebendo o novo executavel diretamente da release oficial.");
                UpdaterEngine.DownloadFile(options.DownloadUrl, downloadPath, delegate(long received, long total)
                {
                    int percent = total > 0 ? 15 + (int)Math.Min(60, received * 60L / total) : 35;
                    SetProgress(percent, "Baixando " + options.Version + "...", total > 0 ? FormatBytes(received) + " de " + FormatBytes(total) : FormatBytes(received));
                });

                SetProgress(78, "Validando o download...", "Conferindo o executavel e sua assinatura SHA-256.");
                UpdaterEngine.ValidateExecutable(downloadPath, options.Sha256);

                SetProgress(88, "Substituindo o aplicativo...", "Instalando no mesmo local do SOLPPE_toolkit.exe.");
                UpdaterEngine.ReplaceTarget(downloadPath, options.TargetPath);
                downloadPath = null;

                SetProgress(100, "Atualizacao concluida", "Reabrindo SOLPPE_toolkit.exe...");
                Thread.Sleep(700);
                Process restarted = Process.Start(new ProcessStartInfo
                {
                    FileName = options.TargetPath,
                    WorkingDirectory = Path.GetDirectoryName(options.TargetPath),
                    UseShellExecute = true
                });

                if (restarted == null)
                {
                    throw new InvalidOperationException("O SOLPPE_toolkit atualizado nao foi iniciado.");
                }

                Thread.Sleep(1200);
                if (restarted.HasExited)
                {
                    throw new InvalidOperationException("O SOLPPE_toolkit atualizado foi encerrado durante a inicializacao. O backup foi mantido.");
                }

                UpdaterEngine.DeletePreviousBackup(options.TargetPath);

                BeginInvoke((MethodInvoker)delegate { Close(); });
            }
            catch (Exception ex)
            {
                if (!String.IsNullOrWhiteSpace(downloadPath))
                {
                    try { if (File.Exists(downloadPath)) File.Delete(downloadPath); } catch { }
                }

                SetFailed(ex.Message);
            }
        }

        private void SetProgress(int value, string status, string detail)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    progressBar.Value = Math.Max(0, Math.Min(100, value));
                    statusLabel.Text = status;
                    detailLabel.Text = detail;
                });
            }
            catch
            {
            }
        }

        private void SetFailed(string message)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    statusLabel.Text = "Nao foi possivel atualizar";
                    statusLabel.ForeColor = Color.FromArgb(176, 44, 44);
                    detailLabel.Text = message;
                    closeButton.Enabled = true;
                });
            }
            catch
            {
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L) return Math.Round(bytes / 1024.0 / 1024.0, 1) + " MB";
            if (bytes >= 1024L) return Math.Round(bytes / 1024.0, 1) + " KB";
            return bytes + " bytes";
        }
    }

    internal static class UpdaterEngine
    {
        public static bool IsAllowedGitHubUri(Uri uri)
        {
            if (uri == null || !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(uri.Host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(uri.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase);
        }

        public static void StopProcess(int processId, string expectedExecutablePath)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return;
                }

                string runningPath = process.MainModule.FileName;
                if (!String.Equals(Path.GetFullPath(runningPath), Path.GetFullPath(expectedExecutablePath), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("O PID informado nao pertence ao SOLPPE_toolkit.exe esperado.");
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            try
            {
                Process killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + processId + " /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (killer != null)
                {
                    killer.WaitForExit(10000);
                }
            }
            catch
            {
            }

            for (int i = 0; i < 40; i++)
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    if (process.HasExited)
                    {
                        return;
                    }
                }
                catch (ArgumentException)
                {
                    return;
                }
                catch
                {
                }

                Thread.Sleep(250);
            }

            throw new InvalidOperationException("O processo anterior do toolkit nao foi encerrado.");
        }

        public static void DownloadFile(string url, string destination, Action<long, long> progress)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "SOLPPE-updater/1.0";
            request.Accept = "application/octet-stream";
            request.AllowAutoRedirect = true;
            request.MaximumAutomaticRedirections = 10;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (!IsAllowedGitHubUri(response.ResponseUri))
                {
                    throw new InvalidDataException("O GitHub redirecionou o download para uma origem nao permitida.");
                }

                long total = response.ContentLength;
                long received = 0;
                byte[] buffer = new byte[128 * 1024];

                using (Stream input = response.GetResponseStream())
                using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        received += read;
                        if (progress != null)
                        {
                            progress(received, total);
                        }
                    }
                    output.Flush(true);
                }
            }
        }

        public static void ValidateExecutable(string path, string expectedSha256)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 65536)
            {
                throw new InvalidDataException("O novo SOLPPE_toolkit.exe foi baixado incompleto.");
            }

            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                {
                    throw new InvalidDataException("O arquivo recebido nao e um executavel Windows valido.");
                }
            }

            string expected = NormalizeSha256(expectedSha256);
            if (String.IsNullOrWhiteSpace(expected))
            {
                throw new InvalidDataException("A release nao informou um SHA-256 valido para o novo toolkit.");
            }

            string actual = ComputeSha256(path);
            if (!String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A verificacao SHA-256 do novo toolkit falhou.");
            }
        }

        public static void ReplaceTarget(string downloadedPath, string targetPath)
        {
            string backupPath = targetPath + ".previous";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            if (!File.Exists(targetPath))
            {
                File.Move(downloadedPath, targetPath);
                return;
            }

            Exception lastError = null;
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    File.Replace(downloadedPath, targetPath, backupPath, true);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Thread.Sleep(250);
                }
            }

            try
            {
                File.Move(targetPath, backupPath);
                try
                {
                    File.Move(downloadedPath, targetPath);
                    return;
                }
                catch
                {
                    if (!File.Exists(targetPath) && File.Exists(backupPath))
                    {
                        File.Move(backupPath, targetPath);
                    }
                    throw;
                }
            }
            catch (Exception fallbackError)
            {
                throw new IOException("Nao foi possivel substituir o executavel atual.", lastError ?? fallbackError);
            }
        }

        public static bool DeletePreviousBackup(string targetPath)
        {
            try
            {
                string backupPath = targetPath + ".previous";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                return !File.Exists(backupPath);
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizeSha256(string digest)
        {
            if (String.IsNullOrWhiteSpace(digest)) return null;
            string value = digest.Trim();
            if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value.Substring(7);
            if (value.Length != 64) return null;
            for (int i = 0; i < value.Length; i++) if (!Uri.IsHexDigit(value[i])) return null;
            return value;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder value = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) value.Append(hash[i].ToString("x2"));
                return value.ToString();
            }
        }

        public static bool RunSelfTest()
        {
            string directory = Path.Combine(Path.GetTempPath(), "SOLPPE_updater_test_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string target = Path.Combine(directory, "SOLPPE_toolkit.exe");
                string download = Path.Combine(directory, "download.tmp");
                File.WriteAllBytes(target, Encoding.ASCII.GetBytes("OLD"));
                byte[] replacement = new byte[70000];
                replacement[0] = (byte)'M';
                replacement[1] = (byte)'Z';
                replacement[replacement.Length - 1] = 123;
                File.WriteAllBytes(download, replacement);
                string sha = ComputeSha256(download);
                ValidateExecutable(download, sha);
                ReplaceTarget(download, target);
                byte[] installed = File.ReadAllBytes(target);
                bool installedCorrectly = installed.Length == replacement.Length && installed[0] == (byte)'M' && installed[installed.Length - 1] == 123;
                bool backupDeleted = DeletePreviousBackup(target) && !File.Exists(target + ".previous");
                return installedCorrectly && backupDeleted;
            }
            catch
            {
                return false;
            }
            finally
            {
                try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
            }
        }
    }
}
