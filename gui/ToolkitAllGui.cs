using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("SOLPPE_toolkit")]
[assembly: AssemblyProduct("SOLPPE_toolkit")]
[assembly: AssemblyCompany("SOLPPE")]
[assembly: AssemblyCopyright("Copyright Natã 2026")]
[assembly: AssemblyVersion("1.0.5.0")]
[assembly: AssemblyFileVersion("1.0.5.0")]

namespace ToolkitAll
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SupportForm());
        }
    }

    internal sealed class SupportForm : Form
    {
        private const string AppVersion = "1.0.5";
        private const string Version = "v1.0";
        private const string DriversVersion = "drivers-impressoras-v1";
        private const string Repo = "Nata-Felix/Instalacao_crystal_adv";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/" + Repo + "/releases/latest";
        private const string ToolkitAssetName = "SOLPPE_toolkit.exe";
        private const string UpdaterAssetName = "SOLPPE_updater.exe";
        private const string BaseUrl = "https://github.com/" + Repo + "/releases/download/" + Version;
        private const string DriversBaseUrl = "https://github.com/" + Repo + "/releases/download/" + DriversVersion;
        private const string DriversIndexUrl = DriversBaseUrl + "/drivers-impressoras.json";
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repo + "/main";
        private const string UrlVersaoNormal = "https://files.tekfarma.com.br/versao/TekFarma50.exe";
        private const string UrlVersaoI = "https://files.tekfarma.com.br/versao/TekFarma50i.exe";
        private const string RadminVpnUrl = "https://download.radmin-vpn.com/download/files/Radmin_VPN_2.0.4899.9.exe";
        private const string AnyDeskUrl = "https://download.anydesk.com/AnyDesk.exe";
        private const string TeamViewerX64Url = "https://download.teamviewer.com/download/TeamViewer_Setup_x64.exe";
        private const string TeamViewerX86Url = "https://download.teamviewer.com/download/TeamViewer_Setup.exe";
        private const string HamachiUrl = "https://vpn.net/";
        private const string WindowsActivationSettingsUri = "ms-settings:activation";
        private const string WindowsActivationHelpUrl = "https://support.microsoft.com/windows/activate-windows-c39005d4-95ee-b91e-b399-2820fda32227";
        private const string OfficeActivationHelpUrl = "https://support.microsoft.com/office/activate-office-5bd38f38-db92-448b-a982-ad170b1e187e";
        private const string OfficeDeploymentToolUrl = "https://www.microsoft.com/download/details.aspx?id=49117";
        private const string CompletedStatus = "Processo concluido";
        private const string FooterSignature = "Vers\u00e3o: " + AppVersion + " - all rights reserved - Developer by Nat\u00e3";

        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color emerald = Color.FromArgb(8, 154, 103);
        private readonly Color emeraldDark = Color.FromArgb(6, 119, 80);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly Color surfaceMuted = Color.FromArgb(243, 248, 250);
        private readonly Color textMuted = Color.FromArgb(89, 111, 125);

        private readonly List<ActionOption> actionOptions = new List<ActionOption>();
        private readonly BrandProgressBar progressBar = new BrandProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly Label etaLabel = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly Button executeButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly CheckBox closeWhenDoneCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly Label versionLabel = new Label();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Panel rootPanel = new Panel();
        private readonly Panel headerPanel = new Panel();
        private readonly PictureBox brandLogo = new PictureBox();
        private readonly Label headerTitleLabel = new Label();
        private readonly Label headerSubtitleLabel = new Label();
        private readonly Label selectLabel = new Label();
        private readonly FlowLayoutPanel actionsPanel = new FlowLayoutPanel();
        private readonly Panel progressCard = new Panel();
        private readonly Panel footerPanel = new Panel();
        private readonly List<CollapsibleSection> actionSections = new List<CollapsibleSection>();
        private CollapsibleSection activeActionSection;

        private BackgroundWorker worker;
        private Process runningProcess;
        private volatile bool cancelRequested;
        private string tempDir;
        private string guiLogPath;
        private int completedUnits;
        private int totalUnits;
        private DateTime executionStartedAt;
        private PrinterDriver selectedPrinter;
        private ActionOption printerActionOption;
        private ActionOption removeDriversActionOption;
        private string selectedMappingHost = "SERVIDOR";
        private ActionOption mappingActionOption;
        private NetworkConfigurationPlan selectedNetworkConfiguration;
        private ActionOption networkConfigurationActionOption;
        private ServerMigrationPlan selectedServerMigration;
        private ActionOption serverMigrationActionOption;
        private SefazTimeZoneOption selectedSefazTimeZone;
        private ActionOption sefazTlsActionOption;
        private LicenseOfficeOption selectedLicenseOfficeOption;
        private OfficeDownloadPlan selectedOfficeDownloadPlan;
        private ActionOption licenseOfficeActionOption;
        private readonly List<string> selectedPrintersToRemove = new List<string>();
        private readonly List<string> selectedPrinterDriversToRemove = new List<string>();
        private volatile bool updateCheckFinished;

        public SupportForm()
        {
            Text = "SOLPPE_toolkit | Central de Suporte";
            ClientSize = new Size(1040, 700);
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = surfaceMuted;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            FormClosing += SupportForm_FormClosing;
            Resize += delegate { ApplyResponsiveLayout(); };

            Icon applicationIcon = LoadEmbeddedIcon("ToolkitAll.Assets.SolppeHandshake.ico");
            if (applicationIcon != null)
            {
                Icon = applicationIcon;
            }

            BuildLayout();
            ApplyResponsiveLayout();
            toolTip.AutoPopDelay = 12000;
            toolTip.InitialDelay = 350;
            toolTip.ReshowDelay = 100;
            Shown += delegate { BeginUpdateCheck(); };
        }

        private void BeginUpdateCheck()
        {
            if (updateCheckFinished)
            {
                return;
            }

            updateCheckFinished = true;
            statusLabel.Text = "Verificando atualizacoes...";

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    CleanupOldUpdaterFiles();
                    CleanupPreviousVersionBackup();
                    UpdateReleaseInfo release = GetLatestToolkitRelease();

                    if (release == null || !IsNewerVersion(release.TagName, AppVersion))
                    {
                        SetUpdateCheckStatus("Pronto para executar", null);
                        return;
                    }

                    if (String.IsNullOrWhiteSpace(release.ToolkitUrl) || String.IsNullOrWhiteSpace(release.UpdaterUrl))
                    {
                        SetUpdateCheckStatus("Pronto para executar", "[ATUALIZACAO] A release " + release.TagName + " nao contem " + ToolkitAssetName + " e " + UpdaterAssetName + ".");
                        return;
                    }

                    if (String.IsNullOrWhiteSpace(NormalizeSha256(release.ToolkitDigest)) || String.IsNullOrWhiteSpace(NormalizeSha256(release.UpdaterDigest)))
                    {
                        SetUpdateCheckStatus("Pronto para executar", "[ATUALIZACAO] A release " + release.TagName + " nao possui verificacao SHA-256 valida para os executaveis.");
                        return;
                    }

                    if (!ConfirmUpdateBeforeDownload(release))
                    {
                        return;
                    }

                    string updaterPath = Path.Combine(Path.GetTempPath(), "SOLPPE_updater_" + SanitizeFilePart(release.TagName) + "_" + Guid.NewGuid().ToString("N") + ".exe");
                    SetUpdateCheckStatus("Baixando atualizador " + release.TagName + "...", "[ATUALIZACAO] Nova versao disponivel: " + release.TagName + ".");
                    DownloadGitHubAsset(release.UpdaterUrl, updaterPath);
                    ValidateDownloadedExecutable(updaterPath, release.UpdaterDigest, UpdaterAssetName);

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || Disposing)
                        {
                            return;
                        }

                        if (worker != null && worker.IsBusy)
                        {
                            TryDeleteFile(updaterPath);
                            AppendLog("[ATUALIZACAO] A instalacao foi adiada porque existe uma tarefa em andamento. Reinicie o toolkit ao concluir.");
                            statusLabel.Text = "Atualizacao disponivel: " + release.TagName;
                            return;
                        }

                        StartUpdaterAndExit(release, updaterPath);
                    });
                }
                catch (Exception ex)
                {
                    SetUpdateCheckStatus("Pronto para executar", "[ATUALIZACAO] Nao foi possivel verificar/aplicar atualizacoes: " + ex.Message);
                }
            });
        }

        private bool ConfirmUpdateBeforeDownload(UpdateReleaseInfo release)
        {
            if (IsDisposed || Disposing || !IsHandleCreated)
            {
                return false;
            }

            bool accepted = false;

            try
            {
                Invoke((MethodInvoker)delegate
                {
                    if (IsDisposed || Disposing)
                    {
                        return;
                    }

                    if (worker != null && worker.IsBusy)
                    {
                        statusLabel.Text = "Atualizacao disponivel: " + release.TagName;
                        AppendLog("[ATUALIZACAO] A confirmacao foi adiada porque existe uma tarefa em andamento.");
                        return;
                    }

                    using (UpdateAvailableDialog dialog = new UpdateAvailableDialog(AppVersion, release.TagName, Icon))
                    {
                        accepted = dialog.ShowDialog(this) == DialogResult.Yes;
                    }

                    if (!accepted)
                    {
                        statusLabel.Text = "Pronto para executar";
                        AppendLog("[ATUALIZACAO] Atualizacao " + release.TagName + " adiada pelo usuario.");
                    }
                });
            }
            catch
            {
                return false;
            }

            return accepted;
        }

        private UpdateReleaseInfo GetLatestToolkitRelease()
        {
            string json;

            using (WebClient client = CreateGitHubClient())
            {
                json = client.DownloadString(LatestReleaseApiUrl);
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.Deserialize<Dictionary<string, object>>(json);
            object tagObject;

            if (root == null || !root.TryGetValue("tag_name", out tagObject))
            {
                return null;
            }

            UpdateReleaseInfo result = new UpdateReleaseInfo();
            result.TagName = Convert.ToString(tagObject);
            object assetsObject;

            if (!root.TryGetValue("assets", out assetsObject))
            {
                return result;
            }

            object[] assets = assetsObject as object[];
            if (assets == null)
            {
                System.Collections.ArrayList list = assetsObject as System.Collections.ArrayList;
                if (list != null)
                {
                    assets = list.ToArray();
                }
            }

            if (assets == null)
            {
                return result;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                if (asset == null)
                {
                    continue;
                }

                string name = GetJsonString(asset, "name");
                string url = GetJsonString(asset, "browser_download_url");
                string digest = GetJsonString(asset, "digest");

                if (String.Equals(name, ToolkitAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    result.ToolkitUrl = url;
                    result.ToolkitDigest = digest;
                }
                else if (String.Equals(name, UpdaterAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    result.UpdaterUrl = url;
                    result.UpdaterDigest = digest;
                }
            }

            return result;
        }

        private static string GetJsonString(Dictionary<string, object> values, string key)
        {
            object value;
            return values != null && values.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : null;
        }

        private static WebClient CreateGitHubClient()
        {
            WebClient client = new WebClient();
            client.Encoding = Encoding.UTF8;
            client.Headers[HttpRequestHeader.UserAgent] = "SOLPPE-toolkit/" + AppVersion;
            client.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            client.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            return client;
        }

        private static void DownloadGitHubAsset(string url, string destination)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || !String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("A URL do atualizador nao pertence ao GitHub.");
            }

            using (WebClient client = CreateGitHubClient())
            {
                client.DownloadFile(uri, destination);
            }
        }

        private static void ValidateDownloadedExecutable(string path, string expectedDigest, string displayName)
        {
            FileInfo file = new FileInfo(path);
            if (!file.Exists || file.Length < 65536)
            {
                throw new InvalidDataException(displayName + " foi baixado incompleto.");
            }

            using (FileStream stream = File.OpenRead(path))
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                {
                    throw new InvalidDataException(displayName + " nao e um executavel valido.");
                }
            }

            string normalizedDigest = NormalizeSha256(expectedDigest);
            if (String.IsNullOrWhiteSpace(normalizedDigest))
            {
                throw new InvalidDataException("A release nao informou o SHA-256 de " + displayName + ".");
            }

            string actual = ComputeSha256(path);
            if (!String.Equals(actual, normalizedDigest, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A verificacao SHA-256 de " + displayName + " falhou.");
            }
        }

        private static string NormalizeSha256(string digest)
        {
            if (String.IsNullOrWhiteSpace(digest))
            {
                return null;
            }

            string value = digest.Trim();
            if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(7);
            }

            if (value.Length != 64)
            {
                return null;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (!Uri.IsHexDigit(value[i]))
                {
                    return null;
                }
            }

            return value;
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    text.Append(hash[i].ToString("x2"));
                }

                return text.ToString();
            }
        }

        private static bool IsNewerVersion(string releaseTag, string currentVersion)
        {
            System.Version release;
            System.Version current;
            return TryParseToolkitVersion(releaseTag, out release) && TryParseToolkitVersion(currentVersion, out current) && release > current;
        }

        private static bool TryParseToolkitVersion(string value, out System.Version version)
        {
            version = null;
            if (String.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string text = value.Trim();
            if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(1);
            }

            int suffix = text.IndexOfAny(new char[] { '-', '+' });
            if (suffix >= 0)
            {
                text = text.Substring(0, suffix);
            }

            string[] parts = text.Split('.');
            if (parts.Length < 1 || parts.Length > 4)
            {
                return false;
            }

            int[] numbers = new int[] { 0, 0, 0, 0 };
            for (int i = 0; i < parts.Length; i++)
            {
                if (!Int32.TryParse(parts[i], out numbers[i]) || numbers[i] < 0)
                {
                    return false;
                }
            }

            version = new System.Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        private void StartUpdaterAndExit(UpdateReleaseInfo release, string updaterPath)
        {
            string targetPath = Path.GetFullPath(Application.ExecutablePath);
            string arguments = "--pid " + Process.GetCurrentProcess().Id +
                " --target " + QuoteArgument(targetPath) +
                " --url " + QuoteArgument(release.ToolkitUrl) +
                " --version " + QuoteArgument(release.TagName) +
                " --sha256 " + QuoteArgument(NormalizeSha256(release.ToolkitDigest) ?? "");

            Process updater = Process.Start(new ProcessStartInfo
            {
                FileName = updaterPath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(targetPath),
                UseShellExecute = true
            });

            if (updater == null)
            {
                throw new InvalidOperationException("O mini instalador nao iniciou.");
            }

            statusLabel.Text = "Instalando atualizacao " + release.TagName + "...";
            AppendLog("[ATUALIZACAO] Mini instalador iniciado. O toolkit sera reaberto automaticamente.");
            Application.Exit();
        }

        private void SetUpdateCheckStatus(string status, string log)
        {
            if (IsDisposed || Disposing)
            {
                return;
            }

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (!IsDisposed && !Disposing)
                    {
                        statusLabel.Text = status;
                        if (!String.IsNullOrWhiteSpace(log))
                        {
                            AppendLog(log);
                        }
                    }
                });
            }
            catch
            {
            }
        }

        private static string SanitizeFilePart(string value)
        {
            StringBuilder result = new StringBuilder();
            string text = value ?? "update";
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                result.Append(Char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_');
            }

            return result.Length == 0 ? "update" : result.ToString();
        }

        private static void CleanupOldUpdaterFiles()
        {
            try
            {
                string[] files = Directory.GetFiles(Path.GetTempPath(), "SOLPPE_updater_*.exe");
                for (int i = 0; i < files.Length; i++)
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(files[i]) < DateTime.UtcNow.AddDays(-2))
                        {
                            File.Delete(files[i]);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        private static void CleanupPreviousVersionBackup()
        {
            try
            {
                string executablePath = Path.GetFullPath(Application.ExecutablePath);
                string backupPath = executablePath + ".previous";
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private sealed class UpdateReleaseInfo
        {
            public string TagName;
            public string ToolkitUrl;
            public string ToolkitDigest;
            public string UpdaterUrl;
            public string UpdaterDigest;
        }

        private void BuildLayout()
        {
            rootPanel.Dock = DockStyle.Fill;
            rootPanel.BackColor = surfaceMuted;
            rootPanel.AutoScroll = true;
            Controls.Add(rootPanel);

            BuildHeader(rootPanel);
            BuildContent(rootPanel);
            BuildFooter(rootPanel);
        }

        private void BuildHeader(Control root)
        {
            headerPanel.Left = 24;
            headerPanel.Top = 16;
            headerPanel.Width = 992;
            headerPanel.Height = 100;
            headerPanel.BackColor = Color.White;
            headerPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(border))
                using (Brush accent = new SolidBrush(emerald))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, headerPanel.Width - 1, headerPanel.Height - 1);
                    e.Graphics.FillRectangle(accent, 0, 0, 5, headerPanel.Height);
                }
            };
            root.Controls.Add(headerPanel);

            brandLogo.Left = 22;
            brandLogo.Top = 12;
            brandLogo.Width = 262;
            brandLogo.Height = 76;
            brandLogo.SizeMode = PictureBoxSizeMode.Zoom;
            brandLogo.BackColor = Color.Transparent;
            brandLogo.Image = TrimTransparentImage(LoadEmbeddedImage("ToolkitAll.Assets.SolppeLogo.png"));
            headerPanel.Controls.Add(brandLogo);

            headerTitleLabel.Text = "Central de suporte empresarial";
            headerTitleLabel.Left = 316;
            headerTitleLabel.Top = 24;
            headerTitleLabel.Width = 630;
            headerTitleLabel.Height = 30;
            headerTitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            headerTitleLabel.Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold, GraphicsUnit.Point);
            headerTitleLabel.ForeColor = darkBlue;
            headerPanel.Controls.Add(headerTitleLabel);

            headerSubtitleLabel.Text = "Solucoes confiaveis para manutencao, instalacao e atendimento tecnico";
            headerSubtitleLabel.Left = 318;
            headerSubtitleLabel.Top = 56;
            headerSubtitleLabel.Width = 620;
            headerSubtitleLabel.Height = 24;
            headerSubtitleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            headerSubtitleLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            headerSubtitleLabel.ForeColor = textMuted;
            headerPanel.Controls.Add(headerSubtitleLabel);
        }

        private void BuildContent(Control root)
        {
            selectLabel.Text = "Solucoes disponiveis";
            selectLabel.Left = 26;
            selectLabel.Top = 130;
            selectLabel.Width = 440;
            selectLabel.Height = 24;
            selectLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            selectLabel.ForeColor = darkBlue;
            root.Controls.Add(selectLabel);

            actionsPanel.Left = 24;
            actionsPanel.Top = 158;
            actionsPanel.Width = 440;
            actionsPanel.Height = 460;
            actionsPanel.AutoScroll = true;
            actionsPanel.FlowDirection = FlowDirection.TopDown;
            actionsPanel.WrapContents = false;
            actionsPanel.Padding = new Padding(7);
            actionsPanel.BackColor = Color.White;
            actionsPanel.BorderStyle = BorderStyle.None;
            actionsPanel.Resize += delegate { ResizeActionSections(); };
            actionsPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(border))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, actionsPanel.Width - 1, actionsPanel.Height - 1);
                }
            };
            root.Controls.Add(actionsPanel);

            int y = 8;
            CollapsibleSection commandSection = AddSection(actionsPanel, "Acesso rapido", SectionIconKind.Command, ref y);
            ActionOption adminCommandAction = AddAction(
                actionsPanel,
                "admcomando",
                "ADM de comando",
                "Abre uma caixa de comandos elevada com pesquisa e sugestoes do Windows.",
                ref y);
            adminCommandAction.CheckBox.CheckedChanged += delegate
            {
                if (!adminCommandAction.CheckBox.Checked)
                {
                    return;
                }

                adminCommandAction.CheckBox.Checked = false;
                ShowAdminCommandDialog();
            };
            commandSection.SetExpanded(true);

            AddSection(actionsPanel, "Rede e acesso", SectionIconKind.Network, ref y);
            networkConfigurationActionOption = AddAction(
                actionsPanel,
                "configuracaorede",
                "Configurar rede avancada",
                "Seleciona o adaptador e permite configurar DHCP, IP fixo, DNS, TLS e reparos de conectividade.",
                ref y);
            networkConfigurationActionOption.CheckBox.CheckedChanged += delegate
            {
                if (networkConfigurationActionOption.CheckBox.Checked && !ShowNetworkConfigurationDialog())
                {
                    networkConfigurationActionOption.CheckBox.Checked = false;
                }
            };
            AddAction(actionsPanel, "rede", "Preparar compartilhamento do Windows", "Ativa servicos, firewall de rede, bindings e parametros de compartilhamento.", ref y);
            AddAction(actionsPanel, "credencial", "Criar credencial SERVIDOR", "Cria credencial SERVIDOR com usuario convidado e senha vazia.", ref y);
            mappingActionOption = AddAction(actionsPanel, "mapear", "Mapear sistema", "Remove mapeamentos antigos, usa host informado e escolhe Z:, Y:, X:...", ref y);
            mappingActionOption.CheckBox.CheckedChanged += delegate
            {
                if (mappingActionOption.CheckBox.Checked && !ShowMappingHostDialog())
                {
                    mappingActionOption.CheckBox.Checked = false;
                }
            };

            AddSection(actionsPanel, "Certificados", SectionIconKind.Certificate, ref y);
            AddAction(actionsPanel, "certificados", "Instalar cadeia de certificado", "Baixa o zip do release e importa .cer, .sst e .p7b em Autoridades Raiz Confiaveis.", ref y);
            sefazTlsActionOption = AddAction(actionsPanel, "ssltlssefaz", "SSL/TLS 1.2 SEFAZ", "Ativa TLS 1.2, configura .NET/WinHTTP, seleciona UTC, sincroniza hora e testa conexao SEFAZ.", ref y);
            sefazTlsActionOption.CheckBox.CheckedChanged += delegate
            {
                if (sefazTlsActionOption.CheckBox.Checked && selectedSefazTimeZone == null)
                {
                    if (!ShowSefazTimeZoneDialog())
                    {
                        sefazTlsActionOption.CheckBox.Checked = false;
                    }
                }
            };

            AddSection(actionsPanel, "Aplicativos", SectionIconKind.Apps, ref y);
            AddAction(actionsPanel, "firewall", "Adicionar excecao no firewall", "Executa os BATs e cria regras para executaveis do sistema encontrados.", ref y);
            AddAction(actionsPanel, "farmaciapopular", "Instalar Farmacia Popular GBAS", "Baixa o GBAS, copia para a pasta do sistema, abre a identificacao do terminal e o portal.", ref y);

            AddSection(actionsPanel, "Softwares", SectionIconKind.Software, ref y);
            AddAction(actionsPanel, "radminvpn", "Instalar Radmin VPN", "Baixa e instala Radmin VPN em modo silencioso, depois abre a interface para criar a rede.", ref y);
            AddAction(actionsPanel, "anydesk", "Baixar e abrir AnyDesk", "Baixa o executavel oficial mais recente do AnyDesk e abre diretamente.", ref y);
            AddAction(actionsPanel, "teamviewer", "Instalar e abrir TeamViewer", "Baixa o Full Client oficial, abre a instalacao e inicia o TeamViewer ao concluir.", ref y);
            AddAction(actionsPanel, "hamachi", "Abrir pagina do Hamachi", "Abre o site oficial do Hamachi para download e informacoes.", ref y);

            AddSection(actionsPanel, "Licencas e Office", SectionIconKind.Windows, ref y);
            licenseOfficeActionOption = AddAction(actionsPanel, "licencasoffice", "Abrir assistente", "Selecione uma opcao para licenca Windows ou instalacao silenciosa do Office pelo CDN oficial.", ref y);
            licenseOfficeActionOption.CheckBox.CheckedChanged += delegate
            {
                if (licenseOfficeActionOption.CheckBox.Checked)
                {
                    if (!ShowLicenseOfficeDialog())
                    {
                        licenseOfficeActionOption.CheckBox.Checked = false;
                    }
                }
            };

            AddSection(actionsPanel, "Servidor", SectionIconKind.Server, ref y);
            AddAction(actionsPanel, "firebird", "Reinstalar Firebird", "Remove Firebird atual, reinstala 2.5.9 e configura recuperacao em 3 tentativas.", ref y);
            serverMigrationActionOption = AddAction(actionsPanel, "trocaservidor", "Troca de servidor", "Assistente para preparar novo servidor ou servidor antigo com fluxo humano.", ref y);
            serverMigrationActionOption.CheckBox.CheckedChanged += delegate
            {
                if (serverMigrationActionOption.CheckBox.Checked)
                {
                    if (!ShowServerMigrationDialog())
                    {
                        serverMigrationActionOption.CheckBox.Checked = false;
                    }
                }
            };

            AddSection(actionsPanel, "Impressoras", SectionIconKind.Printer, ref y);
            printerActionOption = AddAction(actionsPanel, "impressora", "Instalar impressora", "Seleciona marca/modelo, baixa somente o ZIP necessario e abre o instalador.", ref y);
            printerActionOption.CheckBox.CheckedChanged += delegate
            {
                if (printerActionOption.CheckBox.Checked)
                {
                    if (!ShowPrinterSelectionDialog())
                    {
                        printerActionOption.CheckBox.Checked = false;
                    }
                }
            };
            AddAction(actionsPanel, "impressorapdf", "Instalar impressora PDF", "Ativa o recurso nativo Microsoft Print to PDF e recria a impressora quando necessario.", ref y);
            removeDriversActionOption = AddAction(actionsPanel, "removerdrivers", "Remover Drivers", "Seleciona e remove impressoras e drivers instalados sem exigir uma nova instalacao.", ref y);
            removeDriversActionOption.CheckBox.CheckedChanged += delegate
            {
                if (removeDriversActionOption.CheckBox.Checked && !SelectPrinterRemovalItems())
                {
                    removeDriversActionOption.CheckBox.Checked = false;
                }
            };

            AddSection(actionsPanel, "Autonomia Windows", SectionIconKind.Windows, ref y);
            AddAction(actionsPanel, "net35", "Instalar .NET 3.5", "Ativa o recurso NetFX3 pelo DISM, tentando C:\\ e depois Windows Update.", ref y);
            AddAction(actionsPanel, "net48", "Instalar .NET 4.8", "Instala o .NET Framework 4.8 offline usando o instalador do release.", ref y);
            AddAction(actionsPanel, "portacom", "Resetar portas COM", "Remove o ComDB para liberar portas COM reservadas. Pode exigir reinicio.", ref y);
            AddAction(actionsPanel, "removersenhacompartilhamento", "Remover senha de compartilhamento", "Permite uso de senha em branco em compartilhamentos locais.", ref y);
            AddAction(actionsPanel, "windowsupdatefix", "Corrigir Windows Update", "Reinicia componentes, limpa caches e registra DLLs do Windows Update.", ref y);
            AddAction(actionsPanel, "limpezareparowindows", "Limpeza e reparo Windows", "Limpa Temp/Prefetch e executa SFC /scannow e DISM /RestoreHealth.", ref y);
            AddAction(actionsPanel, "cacheicone", "Aumentar cache de icones", "Ajusta Max Cached Icons, limpa cache e reinicia o Explorer.", ref y);
            AddAction(actionsPanel, "firewalloff", "Desativar Firewall", "Desativa o Firewall do Windows em todos os perfis.", ref y);
            AddAction(actionsPanel, "resetimpressora", "Resetar impressora", "Para o spooler, limpa a fila de impressao e inicia o spooler novamente.", ref y);
            AddAction(actionsPanel, "gpedit", "Instalar GPEDIT.MSC", "Instala os pacotes GroupPolicy ClientTools e ClientExtensions via DISM.", ref y);

            BuildProgressPanel(root);
        }

        private CollapsibleSection AddSection(Panel parent, string text, SectionIconKind iconKind, ref int y)
        {
            CollapsibleSection section = new CollapsibleSection(text, iconKind, blue, emerald, border);
            section.Width = Math.Max(280, actionsPanel.ClientSize.Width - 16);
            section.ExpandedChanged += delegate { actionsPanel.PerformLayout(); };
            actionsPanel.Controls.Add(section);
            actionSections.Add(section);
            activeActionSection = section;
            y = 0;
            return section;
        }

        private ActionOption AddAction(Panel parent, string id, string title, string tooltip, ref int y)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = title;
            checkBox.Left = 12;
            checkBox.Top = 0;
            checkBox.Width = 380;
            checkBox.Height = 28;
            checkBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            checkBox.ForeColor = Color.FromArgb(28, 58, 76);
            checkBox.BackColor = Color.White;
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.FlatAppearance.BorderColor = emerald;
            if (activeActionSection == null)
            {
                throw new InvalidOperationException("Uma secao deve ser criada antes das acoes.");
            }
            activeActionSection.AddOption(checkBox);
            toolTip.SetToolTip(checkBox, tooltip);
            ActionOption option = new ActionOption(id, title, checkBox);
            actionOptions.Add(option);
            y = 0;
            return option;
        }

        private void ResizeActionSections()
        {
            int width = Math.Max(280, actionsPanel.ClientSize.Width - actionsPanel.Padding.Horizontal - 12);

            for (int i = 0; i < actionSections.Count; i++)
            {
                actionSections[i].Width = width;
            }
        }

        private Panel ConfigureCard(Panel card, Control root, int left, int top, int width, int height)
        {
            card.Left = left;
            card.Top = top;
            card.Width = width;
            card.Height = height;
            card.BackColor = Color.White;
            card.BorderStyle = BorderStyle.None;
            card.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(border))
                using (Brush accent = new SolidBrush(emerald))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                    e.Graphics.FillRectangle(accent, 0, 0, 4, 48);
                }
            };
            root.Controls.Add(card);
            return card;
        }

        private void BuildProgressPanel(Control root)
        {
            Panel card = ConfigureCard(progressCard, root, 482, 158, 534, 312);

            Label progressTitle = new Label();
            progressTitle.Text = "Acompanhamento da execucao";
            progressTitle.Left = 18;
            progressTitle.Top = 14;
            progressTitle.Width = 360;
            progressTitle.Height = 24;
            progressTitle.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            progressTitle.ForeColor = darkBlue;
            card.Controls.Add(progressTitle);

            progressBar.Left = 18;
            progressBar.Top = 52;
            progressBar.Width = 410;
            progressBar.Height = 28;
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.TrackColor = Color.FromArgb(226, 237, 241);
            progressBar.FillColor = emerald;
            card.Controls.Add(progressBar);

            progressLabel.Text = "0%";
            progressLabel.Left = 446;
            progressLabel.Top = 55;
            progressLabel.Width = 90;
            progressLabel.Height = 24;
            progressLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            progressLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            progressLabel.ForeColor = emeraldDark;
            card.Controls.Add(progressLabel);

            GearPanel gear = new GearPanel();
            gear.Left = 18;
            gear.Top = 96;
            gear.Width = 34;
            gear.Height = 34;
            gear.ForeColor = emerald;
            card.Controls.Add(gear);

            currentStepLabel.Text = "Aguardando inicio do suporte";
            currentStepLabel.Left = 60;
            currentStepLabel.Top = 100;
            currentStepLabel.Width = 470;
            currentStepLabel.Height = 28;
            currentStepLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            currentStepLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            currentStepLabel.ForeColor = darkBlue;
            card.Controls.Add(currentStepLabel);

            etaLabel.Text = "ETA: --  |  Decorrido: 00:00";
            etaLabel.Left = 60;
            etaLabel.Top = 126;
            etaLabel.Width = 470;
            etaLabel.Height = 20;
            etaLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            etaLabel.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            etaLabel.ForeColor = textMuted;
            card.Controls.Add(etaLabel);

            Panel separator = new Panel();
            separator.Left = 18;
            separator.Top = 154;
            separator.Width = 518;
            separator.Height = 1;
            separator.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            separator.BackColor = border;
            card.Controls.Add(separator);

            Label logTitle = new Label();
            logTitle.Text = "Console de execucao";
            logTitle.Left = 18;
            logTitle.Top = 164;
            logTitle.Width = 360;
            logTitle.Height = 24;
            logTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            logTitle.ForeColor = blue;
            card.Controls.Add(logTitle);

            logBox.Left = 18;
            logBox.Top = 192;
            logBox.Width = 518;
            logBox.Height = 98;
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.BackColor = Color.FromArgb(8, 38, 61);
            logBox.ForeColor = Color.FromArgb(213, 241, 230);
            logBox.BorderStyle = BorderStyle.None;
            card.Controls.Add(logBox);
        }

        private void BuildFooter(Control root)
        {
            Panel line = new Panel();
            line.Left = 0;
            line.Top = 0;
            line.Width = 1040;
            line.Height = 3;
            line.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            line.BackColor = emerald;
            footerPanel.Controls.Add(line);

            footerPanel.Left = 0;
            footerPanel.Top = 638;
            footerPanel.Width = 1040;
            footerPanel.Height = 62;
            footerPanel.BackColor = darkBlue;
            root.Controls.Add(footerPanel);

            statusLabel.Text = "Pronto para executar";
            statusLabel.Left = 62;
            statusLabel.Top = 22;
            statusLabel.Width = 300;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.White;
            statusLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            statusLabel.AutoEllipsis = true;
            footerPanel.Controls.Add(statusLabel);

            versionLabel.Text = FooterSignature;
            versionLabel.Left = 210;
            versionLabel.Top = 22;
            versionLabel.Width = 300;
            versionLabel.Height = 22;
            versionLabel.ForeColor = Color.FromArgb(164, 190, 202);
            versionLabel.Font = new Font("Segoe UI", 8F, FontStyle.Regular, GraphicsUnit.Point);
            versionLabel.AutoEllipsis = true;
            footerPanel.Controls.Add(versionLabel);

            InfoCircle info = new InfoCircle();
            info.Left = 40;
            info.Top = 20;
            info.Width = 18;
            info.Height = 18;
            info.ForeColor = emerald;
            footerPanel.Controls.Add(info);

            closeWhenDoneCheckBox.Text = "Fechar ao concluir";
            closeWhenDoneCheckBox.Left = 420;
            closeWhenDoneCheckBox.Top = 18;
            closeWhenDoneCheckBox.Width = 160;
            closeWhenDoneCheckBox.Height = 24;
            closeWhenDoneCheckBox.Checked = false;
            closeWhenDoneCheckBox.ForeColor = Color.FromArgb(221, 235, 241);
            closeWhenDoneCheckBox.BackColor = darkBlue;
            closeWhenDoneCheckBox.FlatStyle = FlatStyle.Flat;
            closeWhenDoneCheckBox.FlatAppearance.BorderColor = emerald;
            closeWhenDoneCheckBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            footerPanel.Controls.Add(closeWhenDoneCheckBox);

            executeButton.Text = "Executar";
            executeButton.Left = 720;
            executeButton.Top = 10;
            executeButton.Width = 130;
            executeButton.Height = 40;
            executeButton.FlatStyle = FlatStyle.Flat;
            executeButton.FlatAppearance.BorderColor = emeraldDark;
            executeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(11, 174, 116);
            executeButton.FlatAppearance.MouseDownBackColor = emeraldDark;
            executeButton.BackColor = emerald;
            executeButton.ForeColor = Color.White;
            executeButton.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            executeButton.Click += delegate { StartSupport(); };
            footerPanel.Controls.Add(executeButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 888;
            cancelButton.Top = 10;
            cancelButton.Width = 108;
            cancelButton.Height = 40;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(92, 126, 147);
            cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(37, 84, 113);
            cancelButton.BackColor = Color.FromArgb(22, 70, 100);
            cancelButton.ForeColor = Color.White;
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelSupport(); };
            footerPanel.Controls.Add(cancelButton);

            closeButton.Text = "Fechar";
            closeButton.Left = 1028;
            closeButton.Top = 10;
            closeButton.Width = 88;
            closeButton.Height = 40;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = Color.White;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 240, 244);
            closeButton.BackColor = Color.White;
            closeButton.ForeColor = darkBlue;
            closeButton.Enabled = true;
            closeButton.Click += delegate { Close(); };
            footerPanel.Controls.Add(closeButton);
        }

        private void ApplyResponsiveLayout()
        {
            if (rootPanel.ClientSize.Width <= 0 || rootPanel.ClientSize.Height <= 0)
            {
                return;
            }

            int viewportWidth = Math.Max(600, rootPanel.ClientSize.Width);
            int viewportHeight = Math.Max(440, rootPanel.ClientSize.Height);
            int margin = 24;
            int gap = 16;
            bool compact = viewportWidth < 900;
            bool compactFooter = viewportWidth < 1020;

            headerPanel.Left = margin;
            headerPanel.Top = 16;
            headerPanel.Width = viewportWidth - (margin * 2);
            headerPanel.Height = 100;

            if (viewportWidth < 760)
            {
                brandLogo.SetBounds(20, 14, 190, 72);
                headerTitleLabel.SetBounds(226, 22, Math.Max(250, headerPanel.Width - 244), 30);
                headerSubtitleLabel.SetBounds(228, 54, Math.Max(246, headerPanel.Width - 248), 34);
                headerSubtitleLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            }
            else
            {
                brandLogo.SetBounds(22, 12, 262, 76);
                headerTitleLabel.SetBounds(316, 24, Math.Max(320, headerPanel.Width - 338), 30);
                headerSubtitleLabel.SetBounds(318, 56, Math.Max(310, headerPanel.Width - 342), 24);
                headerSubtitleLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            }

            selectLabel.Left = margin + 2;
            selectLabel.Top = 130;
            selectLabel.Width = viewportWidth - (margin * 2);

            int contentTop = 158;
            int footerTop;
            int footerHeight;

            if (!compact)
            {
                footerHeight = compactFooter ? 124 : 62;
                int contentHeight = Math.Max(430, viewportHeight - contentTop - footerHeight);
                int availableWidth = viewportWidth - (margin * 2);
                int leftWidth = Math.Max(350, (availableWidth - gap) * 44 / 100);
                int rightLeft = margin + leftWidth + gap;
                int rightWidth = availableWidth - leftWidth - gap;

                actionsPanel.SetBounds(margin, contentTop, leftWidth, contentHeight - 10);
                progressCard.SetBounds(rightLeft, contentTop, rightWidth, contentHeight - 10);

                footerTop = contentTop + contentHeight;
            }
            else
            {
                footerHeight = 124;
                int contentWidth = viewportWidth - (margin * 2);
                int y = contentTop;

                actionsPanel.SetBounds(margin, y, contentWidth, 300);
                y += actionsPanel.Height + gap;
                progressCard.SetBounds(margin, y, contentWidth, 300);
                y += progressCard.Height + gap;
                footerTop = y;
            }

            LayoutFooter(viewportWidth, footerTop, footerHeight, compactFooter);
            rootPanel.AutoScrollMinSize = new Size(0, footerTop + footerHeight);
            ResizeActionSections();
        }

        private void LayoutFooter(int width, int top, int height, bool compact)
        {
            footerPanel.SetBounds(0, top, width, height);

            if (compact)
            {
                statusLabel.SetBounds(62, 12, Math.Max(180, width - 270), 24);
                closeWhenDoneCheckBox.SetBounds(Math.Max(320, width - 188), 10, 160, 24);
                versionLabel.SetBounds(62, 42, Math.Max(300, width - 88), 22);

                closeButton.SetBounds(width - 112, 76, 88, 40);
                cancelButton.SetBounds(closeButton.Left - 120, 76, 108, 40);
                executeButton.SetBounds(cancelButton.Left - 142, 76, 130, 40);
            }
            else
            {
                closeButton.SetBounds(width - 112, 10, 88, 40);
                cancelButton.SetBounds(closeButton.Left - 120, 10, 108, 40);
                executeButton.SetBounds(cancelButton.Left - 142, 10, 130, 40);
                closeWhenDoneCheckBox.SetBounds(Math.Max(320, executeButton.Left - 180), 18, 160, 24);
                statusLabel.SetBounds(62, 22, 138, 24);
                versionLabel.SetBounds(208, 22, Math.Max(180, closeWhenDoneCheckBox.Left - 220), 22);
            }
        }

        private bool ShowMappingHostDialog()
        {
            using (MappingHostDialog dialog = new MappingHostDialog(selectedMappingHost))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedMappingHost = dialog.SelectedHost;
                    toolTip.SetToolTip(mappingActionOption.CheckBox, "Host selecionado: " + selectedMappingHost);
                    return true;
                }
            }

            return false;
        }

        private bool ShowNetworkConfigurationDialog()
        {
            using (NetworkConfigurationDialog dialog = new NetworkConfigurationDialog(selectedNetworkConfiguration))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedPlan != null)
                {
                    selectedNetworkConfiguration = dialog.SelectedPlan;
                    toolTip.SetToolTip(networkConfigurationActionOption.CheckBox, selectedNetworkConfiguration.GetSummary());
                    return true;
                }
            }

            return false;
        }

        private bool ShowPrinterSelectionDialog()
        {
            using (PrinterDriverDialog dialog = new PrinterDriverDialog(DriversIndexUrl, selectedPrinter))
            {
                DialogResult result = dialog.ShowDialog(this);

                if (result == DialogResult.OK && dialog.SelectedDriver != null)
                {
                    selectedPrinter = dialog.SelectedDriver;
                    AskPrinterRemoval();

                    if (printerActionOption != null)
                    {
                        printerActionOption.CheckBox.Checked = true;
                    }

                    UpdatePrinterSelectionState();
                    return true;
                }
            }

            UpdatePrinterSelectionState();
            return false;
        }

        private void AskPrinterRemoval()
        {
            DialogResult answer = MessageBox.Show(
                this,
                "Gostaria de remover alguma impressora ou driver atual antes de instalar?",
                "Remover impressora atual",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                selectedPrintersToRemove.Clear();
                selectedPrinterDriversToRemove.Clear();
                return;
            }

            SelectPrinterRemovalItems();
        }

        private bool SelectPrinterRemovalItems()
        {
            selectedPrintersToRemove.Clear();
            selectedPrinterDriversToRemove.Clear();

            using (PrinterRemovalDialog dialog = new PrinterRemovalDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedPrintersToRemove.AddRange(dialog.SelectedPrinters);
                    selectedPrinterDriversToRemove.AddRange(dialog.SelectedDrivers);
                    if (removeDriversActionOption != null)
                    {
                        toolTip.SetToolTip(
                            removeDriversActionOption.CheckBox,
                            selectedPrintersToRemove.Count + " impressora(s) e " + selectedPrinterDriversToRemove.Count + " driver(s) selecionados.");
                    }

                    return selectedPrintersToRemove.Count > 0 || selectedPrinterDriversToRemove.Count > 0;
                }
            }

            return false;
        }

        private void UpdatePrinterSelectionState()
        {
            if (printerActionOption != null && selectedPrinter != null)
            {
                toolTip.SetToolTip(
                    printerActionOption.CheckBox,
                    "Driver selecionado: " + selectedPrinter.BrandName + " / " + selectedPrinter.ModelName);
            }
        }

        private bool ShowServerMigrationDialog()
        {
            using (ServerMigrationDialog dialog = new ServerMigrationDialog(selectedServerMigration))
            {
                DialogResult result = dialog.ShowDialog(this);

                if (result == DialogResult.OK && dialog.SelectedPlan != null)
                {
                    selectedServerMigration = dialog.SelectedPlan;

                    if (serverMigrationActionOption != null)
                    {
                        serverMigrationActionOption.CheckBox.Checked = true;
                    }

                    UpdateServerMigrationSelectionState();
                    return true;
                }
            }

            UpdateServerMigrationSelectionState();
            return false;
        }

        private bool ShowSefazTimeZoneDialog()
        {
            using (SefazTimeZoneDialog dialog = new SefazTimeZoneDialog(selectedSefazTimeZone))
            {
                DialogResult result = dialog.ShowDialog(this);

                if (result == DialogResult.OK && dialog.SelectedOption != null)
                {
                    selectedSefazTimeZone = dialog.SelectedOption;

                    if (sefazTlsActionOption != null)
                    {
                        sefazTlsActionOption.CheckBox.Checked = true;
                        toolTip.SetToolTip(sefazTlsActionOption.CheckBox, "Fuso selecionado: " + selectedSefazTimeZone.Label);
                    }

                    return true;
                }
            }

            return false;
        }

        private bool ShowLicenseOfficeDialog()
        {
            using (LicenseOfficeDialog dialog = new LicenseOfficeDialog(selectedLicenseOfficeOption))
            {
                DialogResult result = dialog.ShowDialog(this);

                if (result == DialogResult.OK && dialog.SelectedOption != null)
                {
                    selectedLicenseOfficeOption = dialog.SelectedOption;

                    if (String.Equals(selectedLicenseOfficeOption.Kind, "office-download-selector", StringComparison.OrdinalIgnoreCase))
                    {
                        using (OfficeDownloadDialog officeDialog = new OfficeDownloadDialog(selectedOfficeDownloadPlan))
                        {
                            if (officeDialog.ShowDialog(this) != DialogResult.OK || officeDialog.SelectedPlan == null)
                            {
                                selectedLicenseOfficeOption = null;
                                UpdateLicenseOfficeSelectionState();
                                return false;
                            }

                            selectedOfficeDownloadPlan = officeDialog.SelectedPlan;
                        }
                    }

                    if (licenseOfficeActionOption != null)
                    {
                        licenseOfficeActionOption.CheckBox.Checked = true;
                    }

                    UpdateLicenseOfficeSelectionState();
                    return true;
                }
            }

            UpdateLicenseOfficeSelectionState();
            return false;
        }

        private void UpdateLicenseOfficeSelectionState()
        {
            if (licenseOfficeActionOption != null && selectedLicenseOfficeOption != null)
            {
                string text = "Opcao selecionada: " + selectedLicenseOfficeOption.Label;
                if (String.Equals(selectedLicenseOfficeOption.Kind, "office-download-selector", StringComparison.OrdinalIgnoreCase) && selectedOfficeDownloadPlan != null)
                {
                    text += " | " + selectedOfficeDownloadPlan.GetSummary();
                }
                toolTip.SetToolTip(licenseOfficeActionOption.CheckBox, text);
            }
        }

        private void ShowAdminCommandDialog()
        {
            using (AdminCommandDialog dialog = new AdminCommandDialog())
            {
                dialog.ShowDialog(this);
            }
        }

        private void UpdateServerMigrationSelectionState()
        {
            if (serverMigrationActionOption != null && selectedServerMigration != null)
            {
                toolTip.SetToolTip(serverMigrationActionOption.CheckBox, selectedServerMigration.GetSummary());
            }
        }

        private void StartSupport()
        {
            if (worker != null && worker.IsBusy)
            {
                return;
            }

            WorkPlan plan = BuildWorkPlan();
            if (plan == null)
            {
                return;
            }

            if (plan.Actions.Count == 0)
            {
                statusLabel.Text = "Selecione ao menos uma acao";
                currentStepLabel.Text = "Nenhuma acao selecionada";
                return;
            }

            cancelRequested = false;
            completedUnits = 0;
            totalUnits = Math.Max(1, plan.Downloads.Count + plan.Actions.Count);
            executionStartedAt = DateTime.Now;
            progressBar.Value = 0;
            progressLabel.Text = "0%";
            etaLabel.Text = "ETA: calculando...  |  Decorrido: 00:00";
            logBox.Clear();
            statusLabel.Text = "Preparando suporte";
            currentStepLabel.Text = "Preparando arquivos...";
            executeButton.Enabled = false;
            cancelButton.Enabled = true;
            closeButton.Enabled = false;
            SetInputsEnabled(false);

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                ExecutePlan(plan, (BackgroundWorker)sender);
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                SetProgress(e.ProgressPercentage);
                ExecutionProgressInfo progressInfo = e.UserState as ExecutionProgressInfo;
                if (progressInfo != null)
                {
                    currentStepLabel.Text = progressInfo.Message;
                    UpdateEtaDisplay(e.ProgressPercentage, progressInfo);
                }
                else if (e.UserState != null)
                {
                    currentStepLabel.Text = e.UserState.ToString();
                    UpdateEtaDisplay(e.ProgressPercentage, null);
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                runningProcess = null;
                cancelButton.Enabled = false;
                closeButton.Enabled = true;
                executeButton.Enabled = true;
                SetInputsEnabled(true);

                if (cancelRequested)
                {
                    SetProgress(0);
                    currentStepLabel.Text = "Suporte cancelado";
                    statusLabel.Text = "Cancelado pelo usuario";
                    etaLabel.Text = "Cancelado  |  Decorrido: " + FormatDuration(DateTime.Now - executionStartedAt);
                    AppendLog("[AVISO] Suporte cancelado.");
                    return;
                }

                if (e.Error != null)
                {
                    currentStepLabel.Text = "Suporte finalizado com erro";
                    statusLabel.Text = "Erro durante o suporte";
                    etaLabel.Text = "Interrompido  |  Decorrido: " + FormatDuration(DateTime.Now - executionStartedAt);
                    AppendLog("[ERRO] " + e.Error.Message);
                    return;
                }

                SetProgress(100);
                currentStepLabel.Text = "Suporte finalizado";
                statusLabel.Text = CompletedStatus;
                etaLabel.Text = "Concluido em " + FormatDuration(DateTime.Now - executionStartedAt);
                AppendLog("[OK] Processo finalizado.");

                if (closeWhenDoneCheckBox.Checked)
                {
                    BeginInvoke(new Action(Close));
                }
            };
            worker.RunWorkerAsync();
        }

        private WorkPlan BuildWorkPlan()
        {
            WorkPlan plan = new WorkPlan();
            plan.HostServidor = selectedMappingHost == null ? "" : selectedMappingHost.Trim();

            if (String.IsNullOrWhiteSpace(plan.HostServidor))
            {
                plan.HostServidor = "SERVIDOR";
            }

            for (int i = 0; i < actionOptions.Count; i++)
            {
                if (actionOptions[i].CheckBox.Checked)
                {
                    plan.Actions.Add(actionOptions[i]);
                }
            }

            if (plan.ContainsAction("licencasoffice"))
            {
                if (selectedLicenseOfficeOption == null)
                {
                    if (!ShowLicenseOfficeDialog())
                    {
                        statusLabel.Text = "Selecione uma opcao";
                        currentStepLabel.Text = "Assistente de licencas sem opcao selecionada";
                        return null;
                    }
                }

                plan.LicenseOfficeOption = selectedLicenseOfficeOption;
                if (String.Equals(selectedLicenseOfficeOption.Kind, "office-download-selector", StringComparison.OrdinalIgnoreCase))
                {
                    if (selectedOfficeDownloadPlan == null)
                    {
                        statusLabel.Text = "Selecione o Office";
                        currentStepLabel.Text = "Download do Office sem produto selecionado";
                        return null;
                    }
                    plan.OfficeDownloadPlan = selectedOfficeDownloadPlan;
                }
            }

            if (plan.ContainsAction("configuracaorede"))
            {
                if (selectedNetworkConfiguration == null)
                {
                    if (!ShowNetworkConfigurationDialog())
                    {
                        statusLabel.Text = "Configure a rede";
                        currentStepLabel.Text = "Assistente de rede sem configuracao selecionada";
                        return null;
                    }
                }

                plan.NetworkConfiguration = selectedNetworkConfiguration;
            }

            if (PlanHasScriptActions(plan))
            {
                plan.Downloads.Add(new DownloadItem(RawUrl + "/suporte_teksoftware.ps1", "toolkit_all.ps1", "script de suporte"));
            }

            if (plan.ContainsAction("firebird"))
            {
                plan.Downloads.Add(new DownloadItem(BaseUrl + "/Firebird-2.5.9.exe", "Firebird-2.5.9.exe", "Firebird-2.5.9.exe"));
            }

            if (plan.ContainsAction("certificados") || plan.ContainsAction("ssltlssefaz"))
            {
                plan.Downloads.Add(new DownloadItem(BaseUrl + "/CADEIA_CERTIFICADO.zip", "CADEIA_CERTIFICADO.zip", "CADEIA_CERTIFICADO.zip"));
            }

            if (plan.ContainsAction("farmaciapopular"))
            {
                plan.Downloads.Add(new DownloadItem(BaseUrl + "/GBAS_FP_NOVO.zip", "GBAS_FP_NOVO.zip", "GBAS_FP_NOVO.zip"));
            }

            if (plan.ContainsAction("net48"))
            {
                plan.Downloads.Add(new DownloadItem(BaseUrl + "/dotnet48.exe", "dotnet48.exe", "dotnet48.exe"));
            }

            if (plan.ContainsAction("radminvpn"))
            {
                plan.Downloads.Add(new DownloadItem(RadminVpnUrl, "Radmin_VPN_2.0.4899.9.exe", "Radmin VPN"));
            }

            if (plan.ContainsAction("anydesk"))
            {
                plan.Downloads.Add(new DownloadItem(AnyDeskUrl, "AnyDesk.exe", "AnyDesk"));
            }

            if (plan.ContainsAction("teamviewer"))
            {
                string teamViewerUrl = Environment.Is64BitOperatingSystem ? TeamViewerX64Url : TeamViewerX86Url;
                plan.Downloads.Add(new DownloadItem(teamViewerUrl, "TeamViewer_Setup.exe", "TeamViewer Full Client"));
            }

            if (plan.ContainsAction("impressora"))
            {
                if (selectedPrinter == null)
                {
                    if (!ShowPrinterSelectionDialog())
                    {
                        statusLabel.Text = "Selecione a impressora";
                        currentStepLabel.Text = "Instalacao de impressora sem modelo selecionado";
                        return null;
                    }
                }

                plan.PrinterDriver = selectedPrinter;
                plan.Downloads.Add(new DownloadItem(
                    DriversBaseUrl + "/" + selectedPrinter.AssetFile,
                    Path.GetFileName(selectedPrinter.AssetFile),
                    "Driver " + selectedPrinter.BrandName + " " + selectedPrinter.ModelName));
            }

            if (plan.ContainsAction("removerdrivers") &&
                selectedPrintersToRemove.Count == 0 &&
                selectedPrinterDriversToRemove.Count == 0)
            {
                if (!SelectPrinterRemovalItems())
                {
                    statusLabel.Text = "Selecione impressoras ou drivers";
                    currentStepLabel.Text = "Remocao sem itens selecionados";
                    return null;
                }
            }

            if (plan.ContainsAction("impressora") || plan.ContainsAction("removerdrivers"))
            {
                plan.PrintersToRemove.AddRange(selectedPrintersToRemove);
                plan.PrinterDriversToRemove.AddRange(selectedPrinterDriversToRemove);
            }

            if (plan.ContainsAction("trocaservidor"))
            {
                if (selectedServerMigration == null)
                {
                    if (!ShowServerMigrationDialog())
                    {
                        statusLabel.Text = "Configure a troca de servidor";
                        currentStepLabel.Text = "Troca de servidor sem configuracao";
                        return null;
                    }
                }

                plan.ServerMigration = selectedServerMigration;

                if (selectedServerMigration.IsNovoServidor)
                {
                    if (selectedServerMigration.InstalarFull)
                    {
                        plan.Downloads.Add(new DownloadItem(RawUrl + "/instalar.ps1", "instalar.ps1", "instalar.ps1"));
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/CRRuntime_32bit_13_0_39.msi", "CRRuntime_32bit_13_0_39.msi", "CRRuntime_32bit_13_0_39.msi"));
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/crdb_adoplus.zip", "crdb_adoplus.zip", "crdb_adoplus.zip"));
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/dotnet48.exe", "dotnet48.exe", "dotnet48.exe"));
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/VC_redist.x86.exe", "VC_redist.x86.exe", "VC_redist.x86.exe"));
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/VC_redist.x64.exe", "VC_redist.x64.exe", "VC_redist.x64.exe"));

                        if (String.Equals(selectedServerMigration.TipoVersao, "i", StringComparison.OrdinalIgnoreCase))
                        {
                            plan.Downloads.Add(new DownloadItem(UrlVersaoI, "TekFarma50i.exe", "Versao i do sistema"));
                        }
                        else
                        {
                            plan.Downloads.Add(new DownloadItem(UrlVersaoNormal, "TekFarma50.exe", "Versao normal do sistema"));
                        }
                    }

                    if (selectedServerMigration.InstalarFirebird)
                    {
                        plan.Downloads.Add(new DownloadItem(BaseUrl + "/Firebird-2.5.9.exe", "Firebird-2.5.9.exe", "Firebird-2.5.9.exe"));
                    }
                }
            }

            if (plan.ContainsAction("ssltlssefaz"))
            {
                if (selectedSefazTimeZone == null)
                {
                    if (!ShowSefazTimeZoneDialog())
                    {
                        statusLabel.Text = "Selecione o UTC";
                        currentStepLabel.Text = "SSL/TLS SEFAZ sem UTC selecionado";
                        return null;
                    }
                }

                plan.SefazTimeZone = selectedSefazTimeZone;
            }

            return plan;
        }

        private bool PlanHasScriptActions(WorkPlan plan)
        {
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                if (!IsGuiHandledAction(plan.Actions[i].Id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsGuiHandledAction(string id)
        {
            return String.Equals(id, "licencasoffice", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "configuracaorede", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "anydesk", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "teamviewer", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "hamachi", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "impressorapdf", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(id, "removerdrivers", StringComparison.OrdinalIgnoreCase);
        }

        private void ExecutePlan(WorkPlan plan, BackgroundWorker bg)
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "ToolkitAll_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(tempDir);
            guiLogPath = Path.Combine(tempDir, "ToolkitAll_GUI.log");

            AppendLog("[INFO] Pasta temporaria: " + tempDir);

            for (int i = 0; i < plan.Downloads.Count; i++)
            {
                if (cancelRequested) return;

                DownloadItem item = plan.Downloads[i];
                string destination = Path.Combine(tempDir, item.FileName);

                bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Baixando " + item.Name + "...");
                AppendLog("[INFO] Baixando: " + item.Name);
                AppendLog("[INFO] Origem do download preparada.");

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(item.Url, destination);
                }

                FileInfo fi = new FileInfo(destination);
                AppendLog("[OK] " + item.Name + " baixado (" + FormatBytes(fi.Length) + ")");
                completedUnits++;
                bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Download concluido: " + item.Name);
            }

            if (cancelRequested) return;

            if (plan.LicenseOfficeOption != null)
            {
                ExecuteLicenseOfficeOption(plan.LicenseOfficeOption, plan.OfficeDownloadPlan, bg);
                completedUnits++;
            }

            if (plan.NetworkConfiguration != null)
            {
                ApplyNetworkConfiguration(plan.NetworkConfiguration, bg);
                completedUnits++;
                if (cancelRequested) return;
            }

            if (plan.ContainsAction("anydesk"))
            {
                OpenDownloadedApplication("AnyDesk.exe", "AnyDesk", bg);
                completedUnits++;
                if (cancelRequested) return;
            }

            if (plan.ContainsAction("teamviewer"))
            {
                InstallAndOpenTeamViewer(bg);
                completedUnits++;
                if (cancelRequested) return;
            }

            if (plan.ContainsAction("hamachi"))
            {
                bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Abrindo pagina oficial do Hamachi...");
                OpenShellTarget(HamachiUrl, "pagina oficial do Hamachi");
                completedUnits++;
            }

            if (plan.ContainsAction("removerdrivers"))
            {
                RemoveSelectedPrintersAndDrivers(plan, bg);
                completedUnits++;
                if (cancelRequested) return;
            }

            if (plan.ContainsAction("impressorapdf"))
            {
                InstallMicrosoftPrintToPdf(bg);
                completedUnits++;
                if (cancelRequested) return;
            }

            List<string> scriptIds = new List<string>();
            List<string> allIds = plan.GetActionIds();
            for (int i = 0; i < allIds.Count; i++)
            {
                string aid = allIds[i];
                if (!IsGuiHandledAction(aid))
                {
                    scriptIds.Add(aid);
                }
            }

            if (scriptIds.Count == 0)
            {
                bg.ReportProgress(100, "Acoes concluidas");
                return;
            }

            string actionList = String.Join(",", scriptIds.ToArray());
            string scriptPath = Path.Combine(tempDir, "toolkit_all.ps1");
            string args = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(scriptPath) + " -Acoes " + QuoteArgument(actionList) + " -HostServidor " + QuoteArgument(plan.HostServidor);

            if (plan.PrinterDriver != null)
            {
                args += " -ImpressoraMarca " + QuoteArgument(plan.PrinterDriver.BrandName);
                args += " -ImpressoraModelo " + QuoteArgument(plan.PrinterDriver.ModelName);
                args += " -ImpressoraArquivo " + QuoteArgument(Path.GetFileName(plan.PrinterDriver.AssetFile));
                args += " -ImpressoraInstalador " + QuoteArgument(plan.PrinterDriver.InstallerPath);
                if (!plan.ContainsAction("removerdrivers"))
                {
                    args += " -RemoverImpressoras " + QuoteArgument(JoinArgumentList(plan.PrintersToRemove));
                    args += " -RemoverDriversImpressora " + QuoteArgument(JoinArgumentList(plan.PrinterDriversToRemove));
                }
            }

            if (plan.ServerMigration != null)
            {
                args += " -TrocaPerfil " + QuoteArgument(plan.ServerMigration.IsNovoServidor ? "novo" : "antigo");
                args += " -TrocaHostAntigo " + QuoteArgument(plan.ServerMigration.HostAntigo);
                args += " -TrocaTipoVersao " + QuoteArgument(plan.ServerMigration.TipoVersao);
                args += " -TrocaCopiarPrincipal " + QuoteArgument(plan.ServerMigration.CopiarPrincipal ? "true" : "false");
                args += " -TrocaCopiarFinal " + QuoteArgument(plan.ServerMigration.CopiarFinal ? "true" : "false");
                args += " -TrocaInstalarFull " + QuoteArgument(plan.ServerMigration.InstalarFull ? "true" : "false");
                args += " -TrocaInstalarFirebird " + QuoteArgument(plan.ServerMigration.InstalarFirebird ? "true" : "false");
                args += " -TrocaConfigurarRede " + QuoteArgument(plan.ServerMigration.ConfigurarRede ? "true" : "false");
                args += " -TrocaRenomearReiniciar " + QuoteArgument(plan.ServerMigration.RenomearReiniciar ? "true" : "false");
                args += " -TrocaExcluirPastas " + QuoteArgument(plan.ServerMigration.ExcluirPastas);
            }

            if (plan.SefazTimeZone != null)
            {
                args += " -SefazTimeZoneId " + QuoteArgument(plan.SefazTimeZone.TimeZoneId);
            }

            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Executando toolkit...");
            AppendLog("[INFO] Iniciando PowerShell:");
            AppendLog("powershell.exe " + args);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = tempDir;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        AppendLog(e.Data);
                        TrackActionProgress(e.Data, bg);
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!String.IsNullOrEmpty(e.Data))
                    {
                        AppendLog("[ERRO] " + e.Data);
                    }
                };

                runningProcess = process;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.HasExited)
                {
                    if (cancelRequested)
                    {
                        KillRunningProcessTree();
                        return;
                    }

                    Thread.Sleep(250);
                }

                AppendLog("[INFO] PowerShell finalizado. ExitCode: " + process.ExitCode);

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("O script PowerShell terminou com ExitCode " + process.ExitCode + ".");
                }
            }

            bg.ReportProgress(100, "Suporte concluido");
        }

        private void ExecuteLicenseOfficeOption(LicenseOfficeOption option, OfficeDownloadPlan officePlan, BackgroundWorker bg)
        {
            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), option.ProgressText);
            AppendLog("[INFO] " + option.LogText);

            if (String.Equals(option.Kind, "settings", StringComparison.OrdinalIgnoreCase))
            {
                OpenShellTarget(WindowsActivationSettingsUri, "Configuracoes de ativacao do Windows");
                return;
            }

            if (String.Equals(option.Kind, "url", StringComparison.OrdinalIgnoreCase))
            {
                OpenShellTarget(option.Target, option.Label);
                return;
            }

            if (String.Equals(option.Kind, "admin-powershell", StringComparison.OrdinalIgnoreCase))
            {
                OpenElevatedPowerShell(option.Target, option.Label);
                return;
            }

            if (String.Equals(option.Kind, "office-download-selector", StringComparison.OrdinalIgnoreCase))
            {
                if (officePlan == null || officePlan.Items.Count == 0)
                {
                    throw new InvalidOperationException("Nenhum produto Office foi selecionado para instalacao.");
                }

                RunLocalPowerShellAction("Baixando e instalando o Microsoft Office...", BuildOfficeSilentInstallScript(officePlan), bg);
                if (cancelRequested) return;
                AppendLog("[OK] Instalacao silenciosa do Office concluida pela ferramenta oficial da Microsoft.");
                return;
            }

            if (String.Equals(option.Kind, "noop", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog("[INFO] Opcao manual selecionada. Nenhum comando foi executado.");
                return;
            }

            AppendLog("[AVISO] Opcao desconhecida: " + option.Label);
        }

        private string BuildOfficeSilentInstallScript(OfficeDownloadPlan plan)
        {
            if (plan == null || plan.Items.Count == 0)
            {
                throw new InvalidOperationException("Plano de instalacao do Office vazio.");
            }

            string language = String.Equals(plan.LanguageCode, "en-us", StringComparison.OrdinalIgnoreCase) ? "en-us" : "pt-br";
            List<string> productIds = new List<string>();
            List<string> productLabels = new List<string>();
            bool hasCompletePackage = false;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                OfficeDownloadItem item = plan.Items[i];
                if ((item.Year != 2019 && item.Year != 2021) || String.IsNullOrWhiteSpace(item.Code))
                {
                    throw new InvalidOperationException("Produto Office invalido no plano de instalacao.");
                }

                for (int c = 0; c < item.Code.Length; c++)
                {
                    if (!Char.IsLetterOrDigit(item.Code[c]))
                    {
                        throw new InvalidOperationException("Codigo de produto Office invalido.");
                    }
                }

                string productId = item.Code + item.Year + "Retail";
                if (!productIds.Contains(productId)) productIds.Add(productId);
                productLabels.Add(item.Label + " " + item.Year);
                if (String.Equals(item.Code, "ProPlus", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(item.Code, "Professional", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(item.Code, "HomeBusiness", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(item.Code, "HomeStudent", StringComparison.OrdinalIgnoreCase))
                {
                    hasCompletePackage = true;
                }
            }

            int estimatedInstallSeconds = hasCompletePackage ? 360 : Math.Min(600, 180 + Math.Max(0, plan.Items.Count - 1) * 60);

            StringBuilder command = new StringBuilder();
            command.AppendLine("$ErrorActionPreference = 'Stop'");
            command.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            command.AppendLine("[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12");
            command.AppendLine("$productIds = " + BuildPowerShellArray(productIds));
            command.AppendLine("$language = " + PowerShellLiteral(language));
            command.AppendLine("$productLabels = " + BuildPowerShellArray(productLabels));
            command.AppendLine("$estimatedInstallSeconds = " + estimatedInstallSeconds);
            command.AppendLine("$work = Join-Path $env:TEMP ('SOLPPE_Office_' + [Guid]::NewGuid().ToString('N'))");
            command.AppendLine("$odtDir = Join-Path $work 'ODT'");
            command.AppendLine("$officeSource = Join-Path $work 'OfficeSource'");
            command.AppendLine("New-Item -ItemType Directory -Path $odtDir -Force | Out-Null");
            command.AppendLine("New-Item -ItemType Directory -Path $officeSource -Force | Out-Null");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|2|Validando requisitos do Office|-1|'");
            command.AppendLine("Write-Output ('Produtos selecionados: ' + ($productLabels -join ', '))");
            command.AppendLine("Write-Output ('Idioma: ' + $language)");
            command.AppendLine("$officeProcesses = @(Get-Process -Name WINWORD,EXCEL,POWERPNT,OUTLOOK,MSACCESS,MSPUB,VISIO,WINPROJ -ErrorAction SilentlyContinue)");
            command.AppendLine("if ($officeProcesses.Count -gt 0) { throw ('Feche os aplicativos do Office antes de instalar: ' + (($officeProcesses | Select-Object -ExpandProperty ProcessName -Unique) -join ', ')) }");
            command.AppendLine("$systemDriveName = $env:SystemDrive.TrimEnd(':')");
            command.AppendLine("$systemDrive = Get-PSDrive -Name $systemDriveName -ErrorAction Stop");
            command.AppendLine("if ($systemDrive.Free -lt 8GB) { throw 'A instalacao requer pelo menos 8 GB livres na unidade do Windows.' }");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|5|Obtendo a ferramenta oficial da Microsoft|-1|'");
            command.AppendLine("Write-Output 'Localizando a versao atual da Office Deployment Tool no site oficial da Microsoft...'");
            command.AppendLine("$downloadPage = Invoke-WebRequest -UseBasicParsing -Uri " + PowerShellLiteral(OfficeDeploymentToolUrl));
            command.AppendLine("$odtUrl = @($downloadPage.Links | ForEach-Object { $_.href } | Where-Object { $_ -match '^https://download\\.microsoft\\.com/.+officedeploymenttool_.+\\.exe$' }) | Select-Object -First 1");
            command.AppendLine("if ([string]::IsNullOrWhiteSpace([string]$odtUrl)) { throw 'A Microsoft nao retornou o link da Office Deployment Tool.' }");
            command.AppendLine("$odtPackage = Join-Path $work 'OfficeDeploymentTool.exe'");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|8|Baixando a Office Deployment Tool|-1|'");
            command.AppendLine("Write-Output 'Baixando a Office Deployment Tool sem abrir o navegador...'");
            command.AppendLine("Invoke-WebRequest -UseBasicParsing -Uri $odtUrl -OutFile $odtPackage");
            command.AppendLine("$signature = Get-AuthenticodeSignature -FilePath $odtPackage");
            command.AppendLine("if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') { throw 'A assinatura digital da Office Deployment Tool nao e valida.' }");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|12|Assinatura Microsoft validada|-1|'");
            command.AppendLine("Write-Output 'Assinatura digital Microsoft validada.'");
            command.AppendLine("$extractArgs = '/quiet /extract:\"' + $odtDir + '\"'");
            command.AppendLine("$extract = Start-Process -FilePath $odtPackage -ArgumentList $extractArgs -WindowStyle Hidden -Wait -PassThru");
            command.AppendLine("if ($extract.ExitCode -ne 0) { throw ('Falha ao extrair a Office Deployment Tool. Codigo: ' + $extract.ExitCode) }");
            command.AppendLine("$setup = Join-Path $odtDir 'setup.exe'");
            command.AppendLine("if (!(Test-Path -LiteralPath $setup)) { throw 'setup.exe nao encontrado na Office Deployment Tool.' }");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|15|Preparando os arquivos do Office|-1|'");
            command.AppendLine("$edition = '64'");
            command.AppendLine("$clickToRun = Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Office\\ClickToRun\\Configuration' -ErrorAction SilentlyContinue");
            command.AppendLine("if ($clickToRun.Platform -match 'x86|32') { $edition = '32' }");
            command.AppendLine("$productXml = @($productIds | ForEach-Object { '    <Product ID=\"' + $_ + '\">' + [Environment]::NewLine + '      <Language ID=\"' + $language + '\" />' + [Environment]::NewLine + '    </Product>' }) -join [Environment]::NewLine");
            command.AppendLine("$configuration = '<Configuration>' + [Environment]::NewLine + '  <Add OfficeClientEdition=\"' + $edition + '\" SourcePath=\"' + $officeSource + '\" AllowCdnFallback=\"TRUE\">' + [Environment]::NewLine + $productXml + [Environment]::NewLine + '  </Add>' + [Environment]::NewLine + '  <Display Level=\"None\" AcceptEULA=\"TRUE\" />' + [Environment]::NewLine + '  <Updates Enabled=\"TRUE\" />' + [Environment]::NewLine + '  <Property Name=\"FORCEAPPSHUTDOWN\" Value=\"FALSE\" />' + [Environment]::NewLine + '</Configuration>'");
            command.AppendLine("$configurationPath = Join-Path $work 'configuration.xml'");
            command.AppendLine("Set-Content -LiteralPath $configurationPath -Value $configuration -Encoding UTF8");
            command.AppendLine("$doBaseline = @{}");
            command.AppendLine("$doAvailable = $null -ne (Get-Command Get-DeliveryOptimizationStatus -ErrorAction SilentlyContinue)");
            command.AppendLine("if ($doAvailable) { Get-DeliveryOptimizationStatus -ErrorAction SilentlyContinue | Where-Object { $_.PredefinedCallerApplication -eq 'Microsoft Office Click-to-Run' -and $_.SourceUrl -match '(?i)(officecdn|c2r\\.ts\\.cdn\\.office)' } | ForEach-Object { $doBaseline[$_.FileId] = [long]$_.TotalBytesDownloaded } }");
            command.AppendLine("$downloadArgs = '/download \"' + $configurationPath + '\"'");
            command.AppendLine("$download = Start-Process -FilePath $setup -ArgumentList $downloadArgs -WindowStyle Hidden -PassThru");
            command.AppendLine("$downloadStarted = Get-Date");
            command.AppendLine("$lastSampleAt = $downloadStarted");
            command.AppendLine("$lastDownloadedBytes = 0L");
            command.AppendLine("$smoothedBytesPerSecond = 0.0");
            command.AppendLine("$displayDownloadPercent = 0");
            command.AppendLine("while (!$download.HasExited) {");
            command.AppendLine("  Start-Sleep -Seconds 2");
            command.AppendLine("  $now = Get-Date");
            command.AppendLine("  $downloadElapsed = [Math]::Max(1, ($now - $downloadStarted).TotalSeconds)");
            command.AppendLine("  $knownTotalBytes = 0L; $downloadedBytes = 0L");
            command.AppendLine("  if ($doAvailable) {");
            command.AppendLine("    $doItems = @(Get-DeliveryOptimizationStatus -ErrorAction SilentlyContinue | Where-Object { $_.PredefinedCallerApplication -eq 'Microsoft Office Click-to-Run' -and $_.SourceUrl -match '(?i)(officecdn|c2r\\.ts\\.cdn\\.office)' })");
            command.AppendLine("    foreach ($doItem in $doItems) { $baselineBytes = if ($doBaseline.ContainsKey($doItem.FileId)) { [long]$doBaseline[$doItem.FileId] } else { 0L }; $itemSize = [Math]::Max([long]0, ([long]$doItem.FileSize - [Math]::Min([long]$doItem.FileSize, $baselineBytes))); $itemDownloaded = [Math]::Max([long]0, ([long]$doItem.TotalBytesDownloaded - $baselineBytes)); $knownTotalBytes += $itemSize; $downloadedBytes += [Math]::Min($itemSize, $itemDownloaded) }");
            command.AppendLine("  }");
            command.AppendLine("  if ($knownTotalBytes -le 0) { $downloadMeasure = Get-ChildItem -LiteralPath $officeSource -File -Recurse -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum; $downloadedBytes = if ($null -eq $downloadMeasure.Sum) { 0L } else { [long]$downloadMeasure.Sum } }");
            command.AppendLine("  $sampleSeconds = [Math]::Max(0.25, ($now - $lastSampleAt).TotalSeconds)");
            command.AppendLine("  $deltaBytes = [Math]::Max([long]0, ([long]$downloadedBytes - [long]$lastDownloadedBytes))");
            command.AppendLine("  $instantBytesPerSecond = $deltaBytes / $sampleSeconds");
            command.AppendLine("  if ($instantBytesPerSecond -gt 0 -and $instantBytesPerSecond -lt 500MB) { $smoothedBytesPerSecond = if ($smoothedBytesPerSecond -le 0) { $instantBytesPerSecond } else { ($smoothedBytesPerSecond * 0.72) + ($instantBytesPerSecond * 0.28) } }");
            command.AppendLine("  if ($knownTotalBytes -gt 0) { $rawDownloadPercent = [Math]::Min(99, [Math]::Round(($downloadedBytes * 100.0) / $knownTotalBytes)); $displayDownloadPercent = [Math]::Max($displayDownloadPercent, $rawDownloadPercent) }");
            command.AppendLine("  $localPercent = if ($knownTotalBytes -gt 0) { 15 + [int][Math]::Round($displayDownloadPercent * 0.65) } else { [Math]::Min(24, 15 + [int][Math]::Floor($downloadElapsed / 30)) }");
            command.AppendLine("  $downloadEta = if ($knownTotalBytes -ge 500MB -and $knownTotalBytes -gt $downloadedBytes -and $smoothedBytesPerSecond -gt 0.1MB -and $downloadElapsed -ge 15) { [int][Math]::Ceiling(($knownTotalBytes - $downloadedBytes) / $smoothedBytesPerSecond) } else { -1 }");
            command.AppendLine("  $downloadMessage = if ($knownTotalBytes -gt 0) { 'Download do Office - {0:N0} / {1:N0} MB' -f ($downloadedBytes / 1MB), ($knownTotalBytes / 1MB) } else { 'Identificando arquivos do Office - {0:N0} MB' -f ($downloadedBytes / 1MB) }");
            command.AppendLine("  $speedText = if ($smoothedBytesPerSecond -gt 0.1MB) { '{0:N1} MB/s' -f ($smoothedBytesPerSecond / 1MB) } else { 'medindo velocidade' }");
            command.AppendLine("  Write-Output ('SOLPPE_PROGRESS|' + $localPercent + '|' + $downloadMessage + '|' + $downloadEta + '|' + $speedText)");
            command.AppendLine("  $lastDownloadedBytes = $downloadedBytes; $lastSampleAt = $now");
            command.AppendLine("}");
            command.AppendLine("$download.WaitForExit()");
            command.AppendLine("if ($download.ExitCode -ne 0) { throw ('Falha ao baixar os arquivos do Office. Codigo: ' + $download.ExitCode) }");
            command.AppendLine("Write-Output ('SOLPPE_PROGRESS|80|Download do Office concluido|' + $estimatedInstallSeconds + '|preparando instalacao')");
            command.AppendLine("Write-Output ('Iniciando instalacao silenciosa do Office ' + $edition + ' bits.')");
            command.AppendLine("$installArgs = '/configure \"' + $configurationPath + '\"'");
            command.AppendLine("$install = Start-Process -FilePath $setup -ArgumentList $installArgs -WindowStyle Hidden -PassThru");
            command.AppendLine("$installStarted = Get-Date");
            command.AppendLine("while (!$install.HasExited) {");
            command.AppendLine("  Start-Sleep -Seconds 5");
            command.AppendLine("  $installElapsed = [int][Math]::Floor(((Get-Date) - $installStarted).TotalSeconds)");
            command.AppendLine("  $installFraction = [Math]::Min(0.96, $installElapsed / [double]$estimatedInstallSeconds)");
            command.AppendLine("  $localPercent = 80 + [int][Math]::Floor($installFraction * 19)");
            command.AppendLine("  $installEta = [Math]::Max(0, $estimatedInstallSeconds - $installElapsed)");
            command.AppendLine("  $installMessage = if ($installElapsed -gt $estimatedInstallSeconds) { 'Finalizando a instalacao do Office' } else { 'Instalando o Office em segundo plano' }");
            command.AppendLine("  Write-Output ('SOLPPE_PROGRESS|' + $localPercent + '|' + $installMessage + '|' + $installEta + '|fase de instalacao')");
            command.AppendLine("}");
            command.AppendLine("$install.WaitForExit()");
            command.AppendLine("if ($install.ExitCode -ne 0) { throw ('A Office Deployment Tool terminou com o codigo ' + $install.ExitCode + '.') }");
            command.AppendLine("Write-Output 'SOLPPE_PROGRESS|100|Office instalado com sucesso|0|concluido'");
            command.AppendLine("Write-Output 'Office baixado e instalado com sucesso.'");
            command.AppendLine("$cleanupPath = [string]$work; Set-Location -LiteralPath $env:TEMP");
            command.AppendLine("try { if ([IO.Directory]::Exists($cleanupPath)) { [IO.Directory]::Delete($cleanupPath, $true) } } catch { Write-Output ('AVISO: a pasta temporaria podera ser removida depois: ' + $cleanupPath) }");
            return command.ToString();
        }

        private void OpenElevatedPowerShell(string command, string label)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoExit -NoProfile -ExecutionPolicy Bypass -Command " + QuoteArgument(command);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                Process.Start(psi);
                AppendLog("[OK] PowerShell aberto como administrador: " + label);
            }
            catch (Win32Exception ex)
            {
                AppendLog("[AVISO] A elevacao foi cancelada ou nao foi autorizada: " + ex.Message);
            }
            catch (Exception ex)
            {
                AppendLog("[AVISO] Nao foi possivel abrir o PowerShell: " + ex.Message);
            }
        }

        private void OpenShellTarget(string target, string label)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = target;
                psi.UseShellExecute = true;
                Process.Start(psi);
                AppendLog("[OK] Aberto: " + label);
            }
            catch (Exception ex)
            {
                AppendLog("[AVISO] Nao foi possivel abrir " + label + ": " + ex.Message);
            }
        }

        private void OpenDownloadedApplication(string fileName, string label, BackgroundWorker bg)
        {
            string path = Path.Combine(tempDir, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Arquivo baixado nao encontrado.", path);
            }

            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Abrindo " + label + "...");
            AppendLog("[INFO] Abrindo " + label + ": " + path);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = path;
            psi.WorkingDirectory = Path.GetDirectoryName(path);
            psi.UseShellExecute = true;
            Process.Start(psi);
            AppendLog("[OK] " + label + " iniciado.");
        }

        private void InstallAndOpenTeamViewer(BackgroundWorker bg)
        {
            string installerPath = Path.Combine(tempDir, "TeamViewer_Setup.exe");
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("Instalador do TeamViewer nao encontrado.", installerPath);
            }

            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Instale o TeamViewer na janela aberta...");
            AppendLog("[INFO] Abrindo instalador oficial do TeamViewer.");
            AppendLog("[INFO] Conclua a instalacao padrao e aceite os termos apresentados pelo fornecedor.");

            ProcessStartInfo installerInfo = new ProcessStartInfo();
            installerInfo.FileName = installerPath;
            installerInfo.WorkingDirectory = Path.GetDirectoryName(installerPath);
            installerInfo.UseShellExecute = true;

            using (Process installer = Process.Start(installerInfo))
            {
                runningProcess = installer;
                while (installer != null && !installer.HasExited)
                {
                    if (cancelRequested)
                    {
                        KillRunningProcessTree();
                        return;
                    }

                    Thread.Sleep(300);
                }

                if (installer != null)
                {
                    AppendLog("[INFO] Instalador do TeamViewer finalizado. ExitCode: " + installer.ExitCode);
                    if (installer.ExitCode != 0)
                    {
                        throw new InvalidOperationException("A instalacao do TeamViewer terminou com ExitCode " + installer.ExitCode + ".");
                    }
                }
            }

            runningProcess = null;
            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Localizando TeamViewer instalado...");

            string teamViewerPath = "";
            for (int attempt = 0; attempt < 40 && !cancelRequested; attempt++)
            {
                teamViewerPath = FindTeamViewerExecutable();
                if (!String.IsNullOrWhiteSpace(teamViewerPath))
                {
                    break;
                }

                Thread.Sleep(500);
            }

            if (cancelRequested)
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(teamViewerPath))
            {
                throw new FileNotFoundException("A instalacao terminou, mas o executavel do TeamViewer nao foi localizado.");
            }

            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Abrindo TeamViewer...");
            AppendLog("[INFO] Abrindo TeamViewer: " + teamViewerPath);

            ProcessStartInfo teamViewerInfo = new ProcessStartInfo();
            teamViewerInfo.FileName = teamViewerPath;
            teamViewerInfo.WorkingDirectory = Path.GetDirectoryName(teamViewerPath);
            teamViewerInfo.UseShellExecute = true;
            Process.Start(teamViewerInfo);
            AppendLog("[OK] TeamViewer aberto para exibir os dados de acesso.");
        }

        private string FindTeamViewerExecutable()
        {
            List<string> candidates = new List<string>();
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!String.IsNullOrWhiteSpace(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "TeamViewer", "TeamViewer.exe"));
            }

            if (!String.IsNullOrWhiteSpace(programFilesX86))
            {
                candidates.Add(Path.Combine(programFilesX86, "TeamViewer", "TeamViewer.exe"));
            }

            if (!String.IsNullOrWhiteSpace(localAppData))
            {
                candidates.Add(Path.Combine(localAppData, "TeamViewer", "TeamViewer.exe"));
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (File.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return "";
        }

        private void ApplyNetworkConfiguration(NetworkConfigurationPlan plan, BackgroundWorker bg)
        {
            RunLocalPowerShellAction("Aplicando configuracao e reparo de rede...", BuildNetworkConfigurationScript(plan), bg);
        }

        private string BuildNetworkConfigurationScript(NetworkConfigurationPlan plan)
        {
            StringBuilder command = new StringBuilder();
            command.AppendLine("$ErrorActionPreference = 'Stop'");
            command.AppendLine("$ifIndex = " + plan.InterfaceIndex);
            command.AppendLine("$alias = " + PowerShellLiteral(plan.InterfaceAlias));
            command.AppendLine("Write-Output ('Adaptador selecionado: ' + $alias + ' (indice ' + $ifIndex + ')')");

            if (plan.ApplyIpSettings)
            {
                if (plan.UseDhcp)
                {
                    command.AppendLine("Write-Output 'Restaurando IPv4 automatico por DHCP...'");
                    command.AppendLine("Get-NetIPAddress -InterfaceIndex $ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.PrefixOrigin -eq 'Manual' } | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue");
                    command.AppendLine("Get-NetRoute -InterfaceIndex $ifIndex -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue");
                    command.AppendLine("Set-NetIPInterface -InterfaceIndex $ifIndex -AddressFamily IPv4 -Dhcp Enabled -ErrorAction Stop");
                    command.AppendLine("Write-Output 'DHCP habilitado.'");
                }
                else
                {
                    command.AppendLine("Write-Output 'Aplicando endereco IPv4 estatico...'");
                    command.AppendLine("$targetIp = " + PowerShellLiteral(plan.IpAddress));
                    command.AppendLine("$currentOnSelected = Get-NetIPAddress -InterfaceIndex $ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -eq $targetIp } | Select-Object -First 1");
                    command.AppendLine("if (!$currentOnSelected) {");
                    command.AppendLine("    Write-Output ('Verificando se o IPv4 ' + $targetIp + ' ja esta em uso...')");
                    command.AppendLine("    $localOnOtherAdapter = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -eq $targetIp -and $_.InterfaceIndex -ne $ifIndex } | Select-Object -First 1");
                    command.AppendLine("    $pingInUse = Test-Connection -ComputerName $targetIp -Count 1 -Quiet -ErrorAction SilentlyContinue");
                    command.AppendLine("    Start-Sleep -Milliseconds 200");
                    command.AppendLine("    $neighborInUse = Get-NetNeighbor -InterfaceIndex $ifIndex -IPAddress $targetIp -ErrorAction SilentlyContinue | Where-Object { $_.State -notin @('Unreachable','Incomplete') -and ![string]::IsNullOrWhiteSpace([string]$_.LinkLayerAddress) -and $_.LinkLayerAddress -ne '00-00-00-00-00-00' } | Select-Object -First 1");
                    command.AppendLine("    if ($localOnOtherAdapter -or $pingInUse -or $neighborInUse) { throw ('O IPv4 ' + $targetIp + ' ja esta em uso. Nenhuma configuracao foi alterada. Escolha outro endereco.') }");
                    command.AppendLine("    Write-Output 'Nenhuma resposta por Ping ou ARP. Prosseguindo com o IPv4 informado.'");
                    command.AppendLine("} else { Write-Output 'O IPv4 informado ja pertence ao adaptador selecionado.' }");
                    command.AppendLine("Set-NetIPInterface -InterfaceIndex $ifIndex -AddressFamily IPv4 -Dhcp Disabled -ErrorAction Stop");
                    command.AppendLine("Get-NetIPAddress -InterfaceIndex $ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -ne '127.0.0.1' -and $_.IPAddress -notlike '169.254.*' } | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue");
                    command.AppendLine("Get-NetRoute -InterfaceIndex $ifIndex -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue");
                    string newIp = "New-NetIPAddress -InterfaceIndex $ifIndex -AddressFamily IPv4 -IPAddress " + PowerShellLiteral(plan.IpAddress) + " -PrefixLength " + plan.PrefixLength;
                    if (!String.IsNullOrWhiteSpace(plan.Gateway))
                    {
                        newIp += " -DefaultGateway " + PowerShellLiteral(plan.Gateway);
                    }
                    command.AppendLine(newIp + " -ErrorAction Stop | Out-Null");
                    command.AppendLine("Write-Output 'IPv4 estatico aplicado.'");
                }
            }

            if (plan.ApplyDnsSettings)
            {
                if (plan.UseAutomaticDns)
                {
                    command.AppendLine("Write-Output 'Restaurando DNS automatico do adaptador...'");
                    command.AppendLine("Set-DnsClientServerAddress -InterfaceIndex $ifIndex -ResetServerAddresses -ErrorAction Stop");
                }
                else
                {
                    List<string> dnsServers = new List<string>();
                    dnsServers.Add(plan.PrimaryDns);
                    if (!String.IsNullOrWhiteSpace(plan.SecondaryDns)) dnsServers.Add(plan.SecondaryDns);
                    command.AppendLine("$dnsServers = " + BuildPowerShellArray(dnsServers));
                    command.AppendLine("Set-DnsClientServerAddress -InterfaceIndex $ifIndex -ServerAddresses $dnsServers -Validate -ErrorAction Stop");
                    command.AppendLine("Write-Output ('DNS manual aplicado: ' + ($dnsServers -join ', '))");
                }
            }

            if (plan.FlushAndRenewDns)
            {
                command.AppendLine("Write-Output 'Limpando e registrando o cache DNS...'");
                command.AppendLine("& ipconfig.exe /flushdns");
                command.AppendLine("& ipconfig.exe /registerdns");
                if (plan.UseDhcp)
                {
                    command.AppendLine("Write-Output 'Renovando concessao DHCP do adaptador selecionado...'");
                    command.AppendLine("& ipconfig.exe /release $alias");
                    command.AppendLine("& ipconfig.exe /renew $alias");
                }
            }

            if (plan.ResetWinHttpProxy)
            {
                command.AppendLine("Write-Output 'Restaurando proxy WinHTTP...'");
                command.AppendLine("& netsh.exe winhttp reset proxy");
            }

            if (plan.EnableTls12)
            {
                command.AppendLine("Write-Output 'Restaurando configuracoes seguras de TLS 1.2...'");
                command.AppendLine("function Set-ToolkitDword([string]$Path,[string]$Name,[int]$Value) { if (!(Test-Path $Path)) { New-Item -Path $Path -Force | Out-Null }; New-ItemProperty -Path $Path -Name $Name -PropertyType DWord -Value $Value -Force | Out-Null }");
                command.AppendLine("foreach ($role in @('Client','Server')) { $path = 'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\SecurityProviders\\SCHANNEL\\Protocols\\TLS 1.2\\' + $role; Set-ToolkitDword $path 'Enabled' 1; Set-ToolkitDword $path 'DisabledByDefault' 0 }");
                command.AppendLine("foreach ($path in @('HKLM:\\SOFTWARE\\Microsoft\\.NETFramework\\v4.0.30319','HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\.NETFramework\\v4.0.30319')) { Set-ToolkitDword $path 'SchUseStrongCrypto' 1; Set-ToolkitDword $path 'SystemDefaultTlsVersions' 1 }");
                command.AppendLine("foreach ($path in @('HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\WinHttp','HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\\WinHttp')) { $current = (Get-ItemProperty -Path $path -Name 'DefaultSecureProtocols' -ErrorAction SilentlyContinue).DefaultSecureProtocols; if ($null -eq $current) { $current = 0 }; Set-ToolkitDword $path 'DefaultSecureProtocols' ([int]$current -bor 0x800) }");
                command.AppendLine("$internetPath = 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings'; $secure = (Get-ItemProperty -Path $internetPath -Name 'SecureProtocols' -ErrorAction SilentlyContinue).SecureProtocols; if ($null -eq $secure) { $secure = 0 }; Set-ToolkitDword $internetPath 'SecureProtocols' ([int]$secure -bor 0x800)");
                command.AppendLine("Write-Output 'TLS 1.2 habilitado para SChannel, .NET, WinHTTP e Internet Settings.'");
            }

            if (plan.ResetWinsockAndTcpIp)
            {
                command.AppendLine("Write-Output 'Resetando Winsock e pilha TCP/IP do Windows...'");
                command.AppendLine("& netsh.exe winsock reset");
                command.AppendLine("& netsh.exe int ip reset");
                command.AppendLine("Write-Output '[AVISO] Reinicie o Windows para concluir o reset de Winsock/TCP-IP.'");
            }

            if (plan.TestConnectivity)
            {
                command.AppendLine("Write-Output 'Estado IPv4 atual:'");
                command.AppendLine("Get-NetIPConfiguration -InterfaceIndex $ifIndex | Format-List InterfaceAlias,IPv4Address,IPv4DefaultGateway,DNSServer | Out-String | Write-Output");
                command.AppendLine("try { [System.Net.Dns]::GetHostAddresses('www.microsoft.com') | Out-Null; Write-Output '[OK] Resolucao DNS funcionando.' } catch { Write-Output ('[AVISO] Falha no teste DNS: ' + $_.Exception.Message) }");
                command.AppendLine("try { $https = Test-NetConnection -ComputerName 'www.microsoft.com' -Port 443 -InformationLevel Quiet -WarningAction SilentlyContinue; if ($https) { Write-Output '[OK] Conexao HTTPS/TLS na porta 443 funcionando.' } else { Write-Output '[AVISO] Nao foi possivel conectar na porta 443.' } } catch { Write-Output ('[AVISO] Falha no teste HTTPS: ' + $_.Exception.Message) }");
            }

            if (plan.OpenInternetAdvancedOptions)
            {
                command.AppendLine("Write-Output 'Abrindo Propriedades da Internet na guia Avancadas...'");
                command.AppendLine("Start-Process -FilePath 'control.exe' -ArgumentList 'inetcpl.cpl,,6'");
                command.AppendLine("Write-Output '[INFO] Na guia Avancadas, use Restaurar configuracoes avancadas para confirmar a restauracao.'");
            }

            command.AppendLine("Write-Output 'Configuracao e reparo de rede finalizados.'");
            return command.ToString();
        }

        private string PowerShellLiteral(string value)
        {
            return "'" + (value ?? "").Replace("'", "''") + "'";
        }

        private void RemoveSelectedPrintersAndDrivers(WorkPlan plan, BackgroundWorker bg)
        {
            RunLocalPowerShellAction("Removendo impressoras e drivers...", BuildPrinterRemovalScript(plan), bg);
        }

        private string BuildPrinterRemovalScript(WorkPlan plan)
        {
            StringBuilder command = new StringBuilder();
            command.AppendLine("$ErrorActionPreference = 'Continue'");
            command.AppendLine("$printers = " + BuildPowerShellArray(plan.PrintersToRemove));
            command.AppendLine("$drivers = " + BuildPowerShellArray(plan.PrinterDriversToRemove));
            command.AppendLine("function Reset-ToolkitSpooler {");
            command.AppendLine("  Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue");
            command.AppendLine("  $queue = Join-Path $env:SystemRoot 'System32\\spool\\PRINTERS\\*'");
            command.AppendLine("  Remove-Item -Path $queue -Force -ErrorAction SilentlyContinue");
            command.AppendLine("  Start-Service -Name Spooler -ErrorAction SilentlyContinue");
            command.AppendLine("}");
            command.AppendLine("foreach ($name in $printers) {");
            command.AppendLine("  try {");
            command.AppendLine("    if (Get-Printer -Name $name -ErrorAction SilentlyContinue) {");
            command.AppendLine("      Write-Output ('Removendo impressora: ' + $name)");
            command.AppendLine("      Remove-Printer -Name $name -ErrorAction Stop");
            command.AppendLine("      Write-Output ('Impressora removida: ' + $name)");
            command.AppendLine("    } else { Write-Output ('[AVISO] Impressora nao encontrada: ' + $name) }");
            command.AppendLine("  } catch { Write-Output ('[AVISO] Falha ao remover impressora ' + $name + ': ' + $_.Exception.Message) }");
            command.AppendLine("}");
            command.AppendLine("if ($printers.Count -gt 0) { Reset-ToolkitSpooler }");
            command.AppendLine("foreach ($name in $drivers) {");
            command.AppendLine("  try {");
            command.AppendLine("    if (Get-PrinterDriver -Name $name -ErrorAction SilentlyContinue) {");
            command.AppendLine("      Write-Output ('Removendo driver: ' + $name)");
            command.AppendLine("      Remove-PrinterDriver -Name $name -ErrorAction Stop");
            command.AppendLine("      Write-Output ('Driver removido: ' + $name)");
            command.AppendLine("    } else { Write-Output ('[AVISO] Driver nao encontrado: ' + $name) }");
            command.AppendLine("  } catch {");
            command.AppendLine("    Write-Output ('[AVISO] Primeira tentativa falhou para ' + $name + ': ' + $_.Exception.Message)");
            command.AppendLine("    Reset-ToolkitSpooler");
            command.AppendLine("    try {");
            command.AppendLine("      Remove-PrinterDriver -Name $name -ErrorAction Stop");
            command.AppendLine("      Write-Output ('Driver removido apos reset do spooler: ' + $name)");
            command.AppendLine("    } catch { Write-Output ('[AVISO] Nao foi possivel remover driver ' + $name + ': ' + $_.Exception.Message) }");
            command.AppendLine("  }");
            command.AppendLine("}");
            command.AppendLine("Write-Output 'Remocao de impressoras e drivers finalizada.'");
            return command.ToString();
        }

        private void InstallMicrosoftPrintToPdf(BackgroundWorker bg)
        {
            RunLocalPowerShellAction("Instalando impressora PDF...", BuildPrintToPdfScript(), bg);
        }

        private string BuildPrintToPdfScript()
        {
            StringBuilder command = new StringBuilder();
            command.AppendLine("$ErrorActionPreference = 'Stop'");
            command.AppendLine("$featureName = 'Printing-PrintToPDFServices-Features'");
            command.AppendLine("$feature = Get-WindowsOptionalFeature -Online -FeatureName $featureName");
            command.AppendLine("if ($feature.State -ne 'Enabled') {");
            command.AppendLine("  Write-Output 'Ativando recurso Microsoft Print to PDF...'");
            command.AppendLine("  $result = Enable-WindowsOptionalFeature -Online -FeatureName $featureName -All -NoRestart");
            command.AppendLine("  if ($result.RestartNeeded) { Write-Output '[AVISO] O Windows solicitou reinicio para concluir o recurso.' }");
            command.AppendLine("  Start-Sleep -Seconds 2");
            command.AppendLine("}");
            command.AppendLine("$printer = Get-Printer -Name 'Microsoft Print to PDF' -ErrorAction SilentlyContinue");
            command.AppendLine("if (!$printer) {");
            command.AppendLine("  $driver = Get-PrinterDriver -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '(?i)Microsoft.*Print.*PDF' } | Select-Object -First 1");
            command.AppendLine("  if (!$driver) {");
            command.AppendLine("    Add-PrinterDriver -Name 'Microsoft Print To PDF' -ErrorAction Stop");
            command.AppendLine("    $driver = Get-PrinterDriver -Name 'Microsoft Print To PDF' -ErrorAction Stop");
            command.AppendLine("  }");
            command.AppendLine("  Add-Printer -Name 'Microsoft Print to PDF' -DriverName $driver.Name -PortName 'PORTPROMPT:' -ErrorAction Stop");
            command.AppendLine("  Write-Output 'Microsoft Print to PDF instalada.'");
            command.AppendLine("} else { Write-Output 'Microsoft Print to PDF ja esta instalada.' }");
            return command.ToString();
        }

        private void RunLocalPowerShellAction(string progressText, string command, BackgroundWorker bg)
        {
            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), progressText);
            string wrappedCommand =
                "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)\r\n" +
                "$OutputEncoding = [Console]::OutputEncoding\r\n" +
                "$ProgressPreference = 'SilentlyContinue'\r\n" +
                "try {\r\n& {\r\n" + command + "\r\n}\r\n" +
                "} catch {\r\n" +
                "  [Console]::Out.WriteLine(('SOLPPE_ERROR|' + $_.Exception.Message))\r\n" +
                "  exit 1\r\n" +
                "}\r\n";
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(wrappedCommand));

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -NonInteractive -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = String.IsNullOrWhiteSpace(tempDir) ? Path.GetTempPath() : tempDir;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (!String.IsNullOrWhiteSpace(e.Data))
                    {
                        if (e.Data.StartsWith("SOLPPE_ERROR|", StringComparison.Ordinal))
                        {
                            AppendLog("[ERRO] " + e.Data.Substring("SOLPPE_ERROR|".Length));
                            return;
                        }

                        int localPercent;
                        ExecutionProgressInfo progressInfo;
                        if (TryParseExecutionProgress(e.Data, out localPercent, out progressInfo))
                        {
                            int overallPercent = CalcUnitProgress(localPercent);
                            bg.ReportProgress(overallPercent, progressInfo);
                        }
                        else
                        {
                            AppendLog(e.Data);
                        }
                    }
                };
                process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    string errorText = NormalizePowerShellErrorLine(e.Data);
                    if (!String.IsNullOrWhiteSpace(errorText)) AppendLog("[ERRO] " + errorText);
                };

                runningProcess = process;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.HasExited)
                {
                    if (cancelRequested)
                    {
                        KillRunningProcessTree();
                        return;
                    }

                    Thread.Sleep(200);
                }

                process.WaitForExit();
                runningProcess = null;
                AppendLog("[INFO] PowerShell local finalizado. ExitCode: " + process.ExitCode);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("A acao terminou com ExitCode " + process.ExitCode + ".");
                }
            }
        }

        private static string NormalizePowerShellErrorLine(string text)
        {
            if (String.IsNullOrWhiteSpace(text) || String.Equals(text.Trim(), "#< CLIXML", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string value = text.Trim();
            if (value.StartsWith("<Objs", StringComparison.OrdinalIgnoreCase) && value.IndexOf("powershell/2004/04", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (value.IndexOf("S=\"progress\"", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return null;
                }

                value = value.Replace("_x000D__x000A_", Environment.NewLine).Replace("_x000A_", Environment.NewLine);
                value = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", " ");
                value = WebUtility.HtmlDecode(value);
                value = System.Text.RegularExpressions.Regex.Replace(value, "[ \\t]+", " ");
                return value.Trim();
            }

            return value;
        }

        private int CalcUnitProgress(int localPercent)
        {
            if (localPercent < 0) localPercent = 0;
            if (localPercent > 100) localPercent = 100;
            double units = completedUnits + (localPercent / 100.0);
            int value = (int)Math.Round((units * 100.0) / Math.Max(1, totalUnits));
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            return value;
        }

        private bool TryParseExecutionProgress(string line, out int localPercent, out ExecutionProgressInfo progressInfo)
        {
            localPercent = 0;
            progressInfo = null;
            if (String.IsNullOrWhiteSpace(line) || !line.StartsWith("SOLPPE_PROGRESS|", StringComparison.Ordinal)) return false;

            string[] parts = line.Split(new char[] { '|' }, 5);
            if (parts.Length < 4 || !Int32.TryParse(parts[1], out localPercent)) return false;
            int etaSeconds;
            if (!Int32.TryParse(parts[3], out etaSeconds)) etaSeconds = -1;
            string detail = parts.Length >= 5 ? parts[4] : "";
            progressInfo = new ExecutionProgressInfo(parts[2], etaSeconds, detail, true);
            return true;
        }

        private string BuildPowerShellArray(List<string> values)
        {
            List<string> quoted = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                quoted.Add("'" + (values[i] ?? "").Replace("'", "''") + "'");
            }

            return "@(" + String.Join(",", quoted.ToArray()) + ")";
        }

        private void TrackActionProgress(string line, BackgroundWorker bg)
        {
            if (line.IndexOf("FINALIZADO:", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int value = Interlocked.Increment(ref completedUnits);
                bg.ReportProgress(CalcPercent(value, totalUnits), line);
            }
        }

        private void CancelSupport()
        {
            cancelRequested = true;
            statusLabel.Text = "Cancelando...";
            AppendLog("[AVISO] Cancelamento solicitado.");
            KillRunningProcessTree();
        }

        private void SupportForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancelRequested = true;
            KillRunningProcessTree();
        }

        private void KillRunningProcessTree()
        {
            Process process = runningProcess;
            if (process == null)
            {
                return;
            }

            int processId;

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                processId = process.Id;
            }
            catch
            {
                return;
            }

            try
            {
                Process killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + processId + " /F /T",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                if (killer != null)
                {
                    killer.WaitForExit(5000);
                }
            }
            catch
            {
            }
        }

        private int CalcPercent(int done, int total)
        {
            int value = (int)Math.Round((done * 100.0) / Math.Max(1, total));
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            return value;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return Math.Round(bytes / 1024.0 / 1024.0, 2) + " MB";
            }

            if (bytes >= 1024)
            {
                return Math.Round(bytes / 1024.0, 2) + " KB";
            }

            return bytes + " bytes";
        }

        private string QuoteArgument(string value)
        {
            if (value == null)
            {
                value = "";
            }

            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private string JoinArgumentList(List<string> values)
        {
            List<string> parts = new List<string>();

            for (int i = 0; i < values.Count; i++)
            {
                if (!String.IsNullOrWhiteSpace(values[i]))
                {
                    parts.Add(values[i].Trim());
                }
            }

            return String.Join("|||", parts.ToArray());
        }

        private void SetProgress(int value)
        {
            if (value < progressBar.Minimum) value = progressBar.Minimum;
            if (value > progressBar.Maximum) value = progressBar.Maximum;

            progressBar.Value = value;
            progressLabel.Text = value + "%";
        }

        private void UpdateEtaDisplay(int progress, ExecutionProgressInfo progressInfo)
        {
            TimeSpan elapsed = executionStartedAt == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - executionStartedAt;
            string elapsedText = FormatDuration(elapsed);
            if (progress >= 100)
            {
                etaLabel.Text = "Concluido em " + elapsedText;
                return;
            }

            if (progressInfo != null)
            {
                string eta = progressInfo.EtaSeconds >= 0 ? FormatDuration(TimeSpan.FromSeconds(progressInfo.EtaSeconds)) : "calculando...";
                string prefix = progressInfo.IsEstimated ? "ETA estimado: " : "ETA: ";
                etaLabel.Text = prefix + eta;
                if (!String.IsNullOrWhiteSpace(progressInfo.Detail)) etaLabel.Text += "  |  " + progressInfo.Detail;
                etaLabel.Text += "  |  Decorrido: " + elapsedText;
                return;
            }

            if (progress <= 0 || elapsed.TotalSeconds < 2)
            {
                etaLabel.Text = "ETA: calculando...  |  Decorrido: " + elapsedText;
                return;
            }

            double remainingSeconds = elapsed.TotalSeconds * (100.0 - progress) / progress;
            if (remainingSeconds < 0) remainingSeconds = 0;
            if (remainingSeconds > 24 * 60 * 60) remainingSeconds = 24 * 60 * 60;
            etaLabel.Text = "ETA estimado: " + FormatDuration(TimeSpan.FromSeconds(remainingSeconds)) + "  |  Decorrido: " + elapsedText;
        }

        private string FormatDuration(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            if (value.TotalHours >= 1) return ((int)value.TotalHours).ToString("00") + ":" + value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00");
            return ((int)value.TotalMinutes).ToString("00") + ":" + value.Seconds.ToString("00");
        }

        private void AppendLog(string text)
        {
            AppendLog(text, true);
        }

        private void AppendLog(string text, bool writeFile)
        {
            if (logBox.InvokeRequired)
            {
                if (writeFile)
                {
                    WriteGuiLogFile(text);
                }

                logBox.BeginInvoke(new Action<string, bool>(AppendLog), text, false);
                return;
            }

            if (writeFile)
            {
                WriteGuiLogFile(text);
            }

            logBox.AppendText(text + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void WriteGuiLogFile(string text)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(guiLogPath))
                {
                    return;
                }

                File.AppendAllText(
                    guiLogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + text + Environment.NewLine);
            }
            catch
            {
            }
        }

        private void SetInputsEnabled(bool enabled)
        {
            for (int i = 0; i < actionOptions.Count; i++)
            {
                actionOptions[i].CheckBox.Enabled = enabled;
            }

            closeWhenDoneCheckBox.Enabled = enabled;
        }

        private Image TrimTransparentImage(Image source)
        {
            if (source == null)
            {
                return null;
            }

            Bitmap bitmap = new Bitmap(source);
            source.Dispose();
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;

            for (int y = 0; y < bitmap.Height; y += 2)
            {
                for (int x = 0; x < bitmap.Width; x += 2)
                {
                    if (bitmap.GetPixel(x, y).A <= 10)
                    {
                        continue;
                    }

                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }

            if (right < left || bottom < top)
            {
                return bitmap;
            }

            int padding = 12;
            left = Math.Max(0, left - padding);
            top = Math.Max(0, top - padding);
            right = Math.Min(bitmap.Width - 1, right + padding);
            bottom = Math.Min(bitmap.Height - 1, bottom + padding);

            Bitmap trimmed = new Bitmap(
                right - left + 1,
                bottom - top + 1,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(trimmed))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.DrawImage(
                    bitmap,
                    new Rectangle(0, 0, trimmed.Width, trimmed.Height),
                    new Rectangle(left, top, trimmed.Width, trimmed.Height),
                    GraphicsUnit.Pixel);
            }

            bitmap.Dispose();
            return trimmed;
        }

        private Image LoadEmbeddedImage(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                return null;
            }

            return Image.FromStream(stream);
        }

        private Icon LoadEmbeddedIcon(string name)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                return null;
            }

            return new Icon(stream);
        }
    }

    internal sealed class BrandProgressBar : Control
    {
        private int minimum;
        private int maximum = 100;
        private int currentValue;

        public Color TrackColor { get; set; }
        public Color FillColor { get; set; }

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (maximum < minimum) maximum = minimum;
                Value = currentValue;
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = Math.Max(minimum, value);
                Value = currentValue;
            }
        }

        public int Value
        {
            get { return currentValue; }
            set
            {
                currentValue = Math.Max(minimum, Math.Min(maximum, value));
                Invalidate();
            }
        }

        public BrandProgressBar()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            TrackColor = Color.FromArgb(226, 237, 241);
            FillColor = Color.FromArgb(8, 154, 103);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            int radius = Math.Max(4, Math.Min(Height / 2, 10));

            using (System.Drawing.Drawing2D.GraphicsPath trackPath = CreateRoundRectangle(track, radius))
            using (Brush trackBrush = new SolidBrush(TrackColor))
            {
                e.Graphics.FillPath(trackBrush, trackPath);
            }

            double range = Math.Max(1, maximum - minimum);
            int fillWidth = (int)Math.Round(((currentValue - minimum) / range) * track.Width);
            if (fillWidth > 0)
            {
                Rectangle fill = new Rectangle(track.X, track.Y, Math.Min(track.Width, fillWidth), track.Height);
                using (System.Drawing.Drawing2D.GraphicsPath fillPath = CreateRoundRectangle(fill, radius))
                using (Brush fillBrush = new SolidBrush(FillColor))
                {
                    e.Graphics.FillPath(fillBrush, fillPath);
                }
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath CreateRoundRectangle(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(rectangle.Width, rectangle.Height)));
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class CollapsibleSection : Panel
    {
        private const int HeaderHeight = 36;
        private readonly Panel header = new Panel();
        private readonly Panel content = new Panel();
        private readonly Label titleLabel = new Label();
        private readonly Label indicatorLabel = new Label();
        private readonly Color borderColor;
        private readonly Color headerColor = Color.FromArgb(243, 248, 250);
        private readonly Color headerExpandedColor = Color.FromArgb(232, 247, 240);
        private int optionCount;
        private bool expanded;

        public event EventHandler ExpandedChanged;

        public CollapsibleSection(string title, SectionIconKind iconKind, Color accent, Color secondary, Color borderColor)
        {
            this.borderColor = borderColor;
            Height = HeaderHeight;
            Margin = new Padding(4, 3, 4, 1);
            BackColor = Color.White;
            BorderStyle = BorderStyle.None;
            DoubleBuffered = true;

            header.Dock = DockStyle.Top;
            header.Height = HeaderHeight;
            header.BackColor = headerColor;
            header.Cursor = Cursors.Hand;
            Controls.Add(header);

            Panel accentBar = new Panel();
            accentBar.Dock = DockStyle.Left;
            accentBar.Width = 4;
            accentBar.BackColor = secondary;
            accentBar.Cursor = Cursors.Hand;
            accentBar.Click += ToggleExpanded;
            header.Controls.Add(accentBar);

            SectionIcon icon = new SectionIcon(iconKind);
            icon.Left = 14;
            icon.Top = 8;
            icon.Width = 18;
            icon.Height = 18;
            icon.ForeColor = secondary;
            icon.Cursor = Cursors.Hand;
            header.Controls.Add(icon);

            titleLabel.Text = title;
            titleLabel.Left = 42;
            titleLabel.Top = 7;
            titleLabel.Width = 250;
            titleLabel.Height = 22;
            titleLabel.Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = accent;
            titleLabel.Cursor = Cursors.Hand;
            titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.Controls.Add(titleLabel);

            indicatorLabel.Text = ">";
            indicatorLabel.TextAlign = ContentAlignment.MiddleCenter;
            indicatorLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            indicatorLabel.ForeColor = secondary;
            indicatorLabel.Width = 28;
            indicatorLabel.Height = HeaderHeight - 2;
            indicatorLabel.Left = Width - 32;
            indicatorLabel.Top = 0;
            indicatorLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            indicatorLabel.Cursor = Cursors.Hand;
            header.Controls.Add(indicatorLabel);

            content.Left = 0;
            content.Top = HeaderHeight;
            content.Width = Math.Max(1, ClientSize.Width);
            content.Height = 0;
            content.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.BackColor = Color.White;
            content.Visible = false;
            Controls.Add(content);

            header.Click += ToggleExpanded;
            icon.Click += ToggleExpanded;
            titleLabel.Click += ToggleExpanded;
            indicatorLabel.Click += ToggleExpanded;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(borderColor))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }

        public void AddOption(CheckBox checkBox)
        {
            checkBox.Left = 12;
            checkBox.Top = 5 + (optionCount * 32);
            checkBox.Width = Math.Max(120, ClientSize.Width - 24);
            checkBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            content.Controls.Add(checkBox);
            optionCount++;
            content.Height = 10 + (optionCount * 32);

            if (expanded)
            {
                Height = HeaderHeight + content.Height;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            content.Width = Math.Max(1, ClientSize.Width);
            titleLabel.Width = Math.Max(80, ClientSize.Width - 80);
        }

        public void SetExpanded(bool value)
        {
            if (expanded == value)
            {
                return;
            }

            expanded = value;
            content.Visible = expanded;
            indicatorLabel.Text = expanded ? "v" : ">";
            header.BackColor = expanded ? headerExpandedColor : headerColor;
            Height = HeaderHeight + (expanded ? content.Height : 0);
            Invalidate();

            if (Parent != null)
            {
                Parent.PerformLayout();
            }
        }

        private void ToggleExpanded(object sender, EventArgs e)
        {
            SetExpanded(!expanded);

            EventHandler handler = ExpandedChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }

    internal sealed class ActionOption
    {
        public readonly string Id;
        public readonly string Title;
        public readonly CheckBox CheckBox;

        public ActionOption(string id, string title, CheckBox checkBox)
        {
            Id = id;
            Title = title;
            CheckBox = checkBox;
        }
    }

    internal sealed class ExecutionProgressInfo
    {
        public readonly string Message;
        public readonly int EtaSeconds;
        public readonly string Detail;
        public readonly bool IsEstimated;

        public ExecutionProgressInfo(string message, int etaSeconds, string detail, bool isEstimated)
        {
            Message = message ?? "";
            EtaSeconds = etaSeconds;
            Detail = detail ?? "";
            IsEstimated = isEstimated;
        }
    }

    internal sealed class WorkPlan
    {
        public string HostServidor;
        public PrinterDriver PrinterDriver;
        public ServerMigrationPlan ServerMigration;
        public SefazTimeZoneOption SefazTimeZone;
        public LicenseOfficeOption LicenseOfficeOption;
        public OfficeDownloadPlan OfficeDownloadPlan;
        public NetworkConfigurationPlan NetworkConfiguration;
        public readonly List<string> PrintersToRemove = new List<string>();
        public readonly List<string> PrinterDriversToRemove = new List<string>();
        public readonly List<ActionOption> Actions = new List<ActionOption>();
        public readonly List<DownloadItem> Downloads = new List<DownloadItem>();

        public bool ContainsAction(string id)
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                if (String.Equals(Actions[i].Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public List<string> GetActionIds()
        {
            List<string> ids = new List<string>();

            for (int i = 0; i < Actions.Count; i++)
            {
                ids.Add(Actions[i].Id);
            }

            return ids;
        }
    }

    internal sealed class DownloadItem
    {
        public readonly string Url;
        public readonly string FileName;
        public readonly string Name;

        public DownloadItem(string url, string fileName, string name)
        {
            Url = url;
            FileName = fileName;
            Name = name;
        }
    }

    internal sealed class AdminCommandItem
    {
        public readonly string Command;
        public readonly string Description;
        public readonly bool IsKnown;
        public readonly bool KeepConsoleOpen;

        public AdminCommandItem(string command, string description, bool isKnown, bool keepConsoleOpen)
        {
            Command = command;
            Description = description;
            IsKnown = isKnown;
            KeepConsoleOpen = keepConsoleOpen;
        }

        public override string ToString()
        {
            return Command;
        }
    }

    internal static class AdminCommandCatalog
    {
        private static readonly HashSet<string> ConsoleCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "arp", "assoc", "attrib", "bcdedit", "cd", "chkdsk", "cipher", "cls", "cmdkey",
            "compact", "convert", "copy", "date", "del", "dir", "diskpart", "dism", "driverquery",
            "echo", "fc", "find", "findstr", "format", "fsutil", "getmac", "gpresult", "hostname",
            "ipconfig", "label", "md", "mklink", "mode", "more", "mountvol", "move", "net", "netsh",
            "netstat", "nslookup", "openfiles", "path", "pathping", "ping", "pnputil", "powercfg",
            "quser", "qwinsta", "reagentc", "reg", "robocopy", "route", "sc", "schtasks", "set",
            "sfc", "shutdown", "sort", "systeminfo", "takeown", "taskkill", "tasklist", "time", "tracert",
            "tree", "type", "ver", "verify", "where", "whoami", "wmic", "xcopy"
        };

        public static List<AdminCommandItem> Build()
        {
            Dictionary<string, AdminCommandItem> items = new Dictionary<string, AdminCommandItem>(StringComparer.OrdinalIgnoreCase);

            AddKnown(items, "appwiz.cpl", "Programas e Recursos");
            AddKnown(items, "azman.msc", "Gerenciador de Autorizacao");
            AddKnown(items, "calc", "Calculadora");
            AddKnown(items, "certlm.msc", "Certificados do computador local");
            AddKnown(items, "certmgr.msc", "Certificados do usuario atual");
            AddKnown(items, "charmap", "Mapa de caracteres");
            AddKnown(items, "cipher", "Criptografia e limpeza segura de dados NTFS");
            AddKnown(items, "chkdsk", "Verificacao do sistema de arquivos");
            AddKnown(items, "cleanmgr", "Limpeza de disco");
            AddKnown(items, "cmd", "Prompt de Comando");
            AddKnown(items, "compmgmt.msc", "Gerenciamento do Computador");
            AddKnown(items, "control", "Painel de Controle");
            AddKnown(items, "control printers", "Impressoras e dispositivos");
            AddKnown(items, "control userpasswords2", "Contas de usuario avancadas");
            AddKnown(items, "desk.cpl", "Configuracoes de exibicao");
            AddKnown(items, "devmgmt.msc", "Gerenciador de Dispositivos");
            AddKnown(items, "dfrgui", "Otimizar unidades");
            AddKnown(items, "diskmgmt.msc", "Gerenciamento de Disco");
            AddKnown(items, "dism", "Manutencao e reparo de imagens do Windows");
            AddKnown(items, "driverquery", "Lista os drivers instalados");
            AddKnown(items, "dxdiag", "Diagnostico do DirectX");
            AddKnown(items, "eventvwr.msc", "Visualizador de Eventos");
            AddKnown(items, "explorer", "Explorador de Arquivos");
            AddKnown(items, "firewall.cpl", "Firewall do Windows Defender");
            AddKnown(items, "fsmgmt.msc", "Pastas Compartilhadas");
            AddKnown(items, "gpedit.msc", "Editor de Politica de Grupo Local");
            AddKnown(items, "inetcpl.cpl", "Propriedades da Internet");
            AddKnown(items, "intl.cpl", "Regiao");
            AddKnown(items, "ipconfig", "Configuracao de rede IP");
            AddKnown(items, "joy.cpl", "Controladores de jogo");
            AddKnown(items, "lusrmgr.msc", "Usuarios e Grupos Locais");
            AddKnown(items, "magnify", "Lupa do Windows");
            AddKnown(items, "main.cpl", "Propriedades do mouse");
            AddKnown(items, "mmc", "Console de Gerenciamento Microsoft");
            AddKnown(items, "mmsys.cpl", "Configuracoes de som");
            AddKnown(items, "mrt", "Ferramenta de Remocao de Software Mal-Intencionado");
            AddKnown(items, "msconfig", "Configuracao do Sistema");
            AddKnown(items, "msinfo32", "Informacoes do Sistema");
            AddKnown(items, "mstsc", "Conexao de Area de Trabalho Remota");
            AddKnown(items, "ncpa.cpl", "Conexoes de Rede");
            AddKnown(items, "netplwiz", "Contas de usuario");
            AddKnown(items, "netsh", "Configuracao avancada de rede");
            AddKnown(items, "netstat", "Conexoes e portas de rede");
            AddKnown(items, "nslookup", "Consulta e diagnostico de DNS");
            AddKnown(items, "notepad", "Bloco de Notas");
            AddKnown(items, "optionalfeatures", "Recursos opcionais do Windows");
            AddKnown(items, "osk", "Teclado virtual");
            AddKnown(items, "perfmon", "Monitor de Desempenho");
            AddKnown(items, "perfmon.msc", "Monitor de Desempenho");
            AddKnown(items, "powershell", "Windows PowerShell");
            AddKnown(items, "powercfg.cpl", "Opcoes de Energia");
            AddKnown(items, "ping", "Teste de conectividade de rede");
            AddKnown(items, "pnputil", "Gerenciamento de drivers pelo terminal");
            AddKnown(items, "printmanagement.msc", "Gerenciamento de Impressao");
            AddKnown(items, "regedit", "Editor do Registro");
            AddKnown(items, "resmon", "Monitor de Recursos");
            AddKnown(items, "rstrui", "Restauracao do Sistema");
            AddKnown(items, "rsop.msc", "Conjunto de Politicas Resultante");
            AddKnown(items, "secpol.msc", "Politica de Seguranca Local");
            AddKnown(items, "services.msc", "Servicos do Windows");
            AddKnown(items, "sfc", "Verificador de arquivos do sistema");
            AddKnown(items, "shell:appsfolder", "Todos os aplicativos instalados");
            AddKnown(items, "shell:downloads", "Pasta Downloads");
            AddKnown(items, "shell:sendto", "Pasta Enviar para");
            AddKnown(items, "shell:startup", "Pasta de inicializacao do usuario");
            AddKnown(items, "shutdown", "Desligar ou reiniciar o Windows");
            AddKnown(items, "snippingtool", "Ferramenta de Captura");
            AddKnown(items, "sysdm.cpl", "Propriedades do Sistema");
            AddKnown(items, "systeminfo", "Informacoes detalhadas do Windows");
            AddKnown(items, "systempropertiesadvanced", "Propriedades avancadas do sistema");
            AddKnown(items, "systempropertiesprotection", "Protecao do Sistema");
            AddKnown(items, "taskmgr", "Gerenciador de Tarefas");
            AddKnown(items, "tasklist", "Lista os processos em execucao");
            AddKnown(items, "tracert", "Rastreia a rota ate um destino de rede");
            AddKnown(items, "taskschd.msc", "Agendador de Tarefas");
            AddKnown(items, "timedate.cpl", "Data e Hora");
            AddKnown(items, "tpm.msc", "Gerenciamento do TPM");
            AddKnown(items, "wf.msc", "Firewall com Seguranca Avancada");
            AddKnown(items, "winver", "Versao do Windows");
            AddKnown(items, "whoami", "Identidade e grupos do usuario atual");
            AddKnown(items, "wmimgmt.msc", "Controle do WMI");

            DiscoverDirectory(items, Environment.SystemDirectory);
            DiscoverDirectory(items, Environment.GetFolderPath(Environment.SpecialFolder.Windows));

            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] pathDirectories = pathValue.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pathDirectories.Length; i++)
            {
                string directory = Environment.ExpandEnvironmentVariables(pathDirectories[i]).Trim().Trim('"');
                if (!directory.StartsWith(@"\\", StringComparison.Ordinal) && Directory.Exists(directory))
                {
                    DiscoverDirectory(items, directory);
                }
            }

            List<AdminCommandItem> result = new List<AdminCommandItem>(items.Values);
            result.Sort(delegate(AdminCommandItem left, AdminCommandItem right)
            {
                if (left.IsKnown != right.IsKnown)
                {
                    return left.IsKnown ? -1 : 1;
                }

                return StringComparer.CurrentCultureIgnoreCase.Compare(left.Command, right.Command);
            });
            return result;
        }

        private static void AddKnown(Dictionary<string, AdminCommandItem> items, string command, string description)
        {
            items[command] = new AdminCommandItem(command, description, true, IsConsoleCommand(command));
        }

        public static bool IsConsoleCommand(string fileName)
        {
            if (String.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string name = Path.GetFileNameWithoutExtension(fileName.Trim());
            return ConsoleCommands.Contains(name);
        }

        private static void DiscoverDirectory(Dictionary<string, AdminCommandItem> items, string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            string[] extensions = new string[] { "*.msc", "*.cpl", "*.exe", "*.com", "*.cmd", "*.bat" };
            for (int i = 0; i < extensions.Length; i++)
            {
                try
                {
                    string[] files = Directory.GetFiles(directory, extensions[i], SearchOption.TopDirectoryOnly);
                    for (int j = 0; j < files.Length; j++)
                    {
                        string extension = Path.GetExtension(files[j]);
                        string command = String.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase)
                            ? Path.GetFileNameWithoutExtension(files[j])
                            : Path.GetFileName(files[j]);

                        if (String.IsNullOrWhiteSpace(command) || items.ContainsKey(command))
                        {
                            continue;
                        }

                        string description = String.Equals(extension, ".msc", StringComparison.OrdinalIgnoreCase)
                            ? "Console de gerenciamento detectado no Windows"
                            : String.Equals(extension, ".cpl", StringComparison.OrdinalIgnoreCase)
                                ? "Painel de controle detectado no Windows"
                                : "Comando detectado; pode exigir argumentos ou nao possuir janela";
                        items.Add(command, new AdminCommandItem(command, description, false, IsConsoleCommand(command)));
                    }
                }
                catch
                {
                }
            }
        }
    }

    internal sealed class UpdateAvailableDialog : Form
    {
        public UpdateAvailableDialog(string currentVersion, string availableVersion, Icon applicationIcon)
        {
            Text = "SOLPPE | Atualizacao disponivel";
            ClientSize = new Size(570, 300);
            MinimumSize = MaximumSize = Size;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            if (applicationIcon != null)
            {
                try { Icon = (Icon)applicationIcon.Clone(); } catch { }
            }

            Controls.Add(SolppeDialogBrand.CreateHeader(ClientSize.Width, "Atualizacao disponivel", "Uma nova versao do SOLPPE_toolkit foi encontrada"));

            Label versionLabel = new Label();
            versionLabel.Text = "Versao instalada: " + currentVersion + "     Nova versao: " + availableVersion;
            versionLabel.SetBounds(28, 112, 514, 28);
            versionLabel.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold, GraphicsUnit.Point);
            versionLabel.ForeColor = Color.FromArgb(7, 45, 75);
            Controls.Add(versionLabel);

            Label messageLabel = new Label();
            messageLabel.Text = "Deseja baixar e instalar a atualizacao agora?\r\nO toolkit sera fechado e reaberto automaticamente.";
            messageLabel.SetBounds(28, 151, 514, 54);
            messageLabel.ForeColor = Color.FromArgb(89, 111, 125);
            Controls.Add(messageLabel);

            Button laterButton = new Button();
            laterButton.Text = "Agora nao";
            laterButton.SetBounds(320, 232, 104, 40);
            laterButton.DialogResult = DialogResult.No;
            laterButton.FlatStyle = FlatStyle.Flat;
            laterButton.BackColor = Color.White;
            laterButton.ForeColor = Color.FromArgb(7, 45, 75);
            laterButton.FlatAppearance.BorderColor = Color.FromArgb(160, 185, 197);
            Controls.Add(laterButton);

            Button updateButton = new Button();
            updateButton.Text = "Atualizar agora";
            updateButton.SetBounds(434, 232, 108, 40);
            updateButton.DialogResult = DialogResult.Yes;
            updateButton.FlatStyle = FlatStyle.Flat;
            updateButton.FlatAppearance.BorderSize = 0;
            updateButton.BackColor = Color.FromArgb(8, 154, 103);
            updateButton.ForeColor = Color.White;
            updateButton.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            Controls.Add(updateButton);

            AcceptButton = updateButton;
            CancelButton = laterButton;
        }
    }

    internal sealed class AdminCommandDialog : Form
    {
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly TextBox commandTextBox = new TextBox();
        private readonly ListBox suggestionList = new ListBox();
        private readonly Label detailLabel = new Label();
        private readonly Button executeButton = new Button();
        private readonly List<AdminCommandItem> allCommands;

        public AdminCommandDialog()
        {
            Text = "ADM de comando";
            ClientSize = new Size(720, 510);
            MinimumSize = new Size(560, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;

            Label title = new Label();
            title.Text = "ADM de comando";
            title.Left = 24;
            title.Top = 20;
            title.Width = 430;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 17F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            Label adminState = new Label();
            adminState.Text = IsAdministrator() ? "Administrador" : "Solicitara elevacao";
            adminState.Left = 548;
            adminState.Top = 25;
            adminState.Width = 148;
            adminState.Height = 26;
            adminState.TextAlign = ContentAlignment.MiddleCenter;
            adminState.BackColor = Color.FromArgb(240, 246, 242);
            adminState.ForeColor = Color.FromArgb(31, 105, 65);
            adminState.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            adminState.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            Controls.Add(adminState);

            Label instruction = new Label();
            instruction.Text = "Digite um comando, nome de ferramenta, caminho ou URL";
            instruction.Left = 26;
            instruction.Top = 66;
            instruction.Width = 660;
            instruction.Height = 22;
            instruction.ForeColor = Color.FromArgb(104, 112, 124);
            instruction.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(instruction);

            commandTextBox.Left = 24;
            commandTextBox.Top = 94;
            commandTextBox.Width = 672;
            commandTextBox.Height = 32;
            commandTextBox.Font = new Font("Consolas", 12F, FontStyle.Regular, GraphicsUnit.Point);
            commandTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            commandTextBox.TextChanged += delegate { RefreshSuggestions(); };
            commandTextBox.KeyDown += CommandTextBox_KeyDown;
            Controls.Add(commandTextBox);

            suggestionList.Left = 24;
            suggestionList.Top = 136;
            suggestionList.Width = 672;
            suggestionList.Height = 258;
            suggestionList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            suggestionList.BorderStyle = BorderStyle.FixedSingle;
            suggestionList.DrawMode = DrawMode.OwnerDrawFixed;
            suggestionList.ItemHeight = 42;
            suggestionList.DrawItem += SuggestionList_DrawItem;
            suggestionList.SelectedIndexChanged += delegate { UpdateDetail(); };
            suggestionList.DoubleClick += delegate { ExecuteSelectedOrTyped(); };
            suggestionList.KeyDown += SuggestionList_KeyDown;
            Controls.Add(suggestionList);

            detailLabel.Left = 26;
            detailLabel.Top = 404;
            detailLabel.Width = 450;
            detailLabel.Height = 54;
            detailLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            detailLabel.ForeColor = Color.FromArgb(104, 112, 124);
            Controls.Add(detailLabel);

            executeButton.Text = "Executar";
            executeButton.Left = 488;
            executeButton.Top = 448;
            executeButton.Width = 100;
            executeButton.Height = 38;
            executeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            executeButton.FlatStyle = FlatStyle.Flat;
            executeButton.FlatAppearance.BorderColor = blue;
            executeButton.BackColor = blue;
            executeButton.ForeColor = Color.White;
            executeButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            executeButton.Click += delegate { ExecuteSelectedOrTyped(); };
            Controls.Add(executeButton);

            Button closeButton = new Button();
            closeButton.Text = "Fechar";
            closeButton.Left = 596;
            closeButton.Top = 448;
            closeButton.Width = 100;
            closeButton.Height = 38;
            closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = border;
            closeButton.BackColor = Color.White;
            closeButton.DialogResult = DialogResult.Cancel;
            Controls.Add(closeButton);

            CancelButton = closeButton;
            Shown += delegate { commandTextBox.Focus(); };

            allCommands = AdminCommandCatalog.Build();
            RefreshSuggestions();
        }

        private void RefreshSuggestions()
        {
            if (allCommands == null)
            {
                return;
            }

            string query = commandTextBox.Text.Trim();
            List<AdminCommandItem> matches = new List<AdminCommandItem>();
            for (int i = 0; i < allCommands.Count; i++)
            {
                AdminCommandItem item = allCommands[i];
                if (query.Length == 0 ||
                    item.Command.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matches.Add(item);
                }
            }

            matches.Sort(delegate(AdminCommandItem left, AdminCommandItem right)
            {
                int leftRank = GetMatchRank(left, query);
                int rightRank = GetMatchRank(right, query);
                int rankCompare = leftRank.CompareTo(rightRank);
                return rankCompare != 0
                    ? rankCompare
                    : StringComparer.CurrentCultureIgnoreCase.Compare(left.Command, right.Command);
            });

            suggestionList.BeginUpdate();
            suggestionList.Items.Clear();
            int limit = Math.Min(query.Length == 0 ? 24 : 60, matches.Count);
            for (int i = 0; i < limit; i++)
            {
                suggestionList.Items.Add(matches[i]);
            }
            suggestionList.SelectedIndex = -1;
            suggestionList.EndUpdate();

            detailLabel.Text = query.Length == 0
                ? allCommands.Count + " comandos disponiveis. Digite para pesquisar."
                : matches.Count + " resultado(s). Use as setas e pressione Enter.";
        }

        private int GetMatchRank(AdminCommandItem item, string query)
        {
            if (query.Length == 0)
            {
                return item.IsKnown ? 0 : 1;
            }

            if (item.Command.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 0;
            if (item.Command.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 2;
        }

        private void CommandTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
            {
                MoveSelection(e.KeyCode == Keys.Down ? 1 : -1);
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelectedOrTyped();
                e.SuppressKeyPress = true;
            }
        }

        private void SuggestionList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelectedOrTyped();
                e.SuppressKeyPress = true;
            }
        }

        private void MoveSelection(int direction)
        {
            if (suggestionList.Items.Count == 0)
            {
                return;
            }

            int next = suggestionList.SelectedIndex;
            if (next < 0)
            {
                next = direction > 0 ? 0 : suggestionList.Items.Count - 1;
            }
            else
            {
                next = Math.Max(0, Math.Min(suggestionList.Items.Count - 1, next + direction));
            }

            suggestionList.SelectedIndex = next;
            suggestionList.TopIndex = Math.Max(0, next - 2);
        }

        private void UpdateDetail()
        {
            AdminCommandItem item = suggestionList.SelectedItem as AdminCommandItem;
            if (item != null)
            {
                detailLabel.Text = item.Command + Environment.NewLine + item.Description;
            }
        }

        private void SuggestionList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= suggestionList.Items.Count)
            {
                return;
            }

            e.DrawBackground();
            AdminCommandItem item = suggestionList.Items[e.Index] as AdminCommandItem;
            if (item == null)
            {
                return;
            }

            Color foreground = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : darkBlue;
            Color secondary = (e.State & DrawItemState.Selected) == DrawItemState.Selected
                ? SystemColors.HighlightText
                : Color.FromArgb(104, 112, 124);

            using (Font commandFont = new Font("Consolas", 10.5F, FontStyle.Bold, GraphicsUnit.Point))
            using (Brush commandBrush = new SolidBrush(foreground))
            using (Brush descriptionBrush = new SolidBrush(secondary))
            {
                e.Graphics.DrawString(item.Command, commandFont, commandBrush, e.Bounds.Left + 10, e.Bounds.Top + 4);
                e.Graphics.DrawString(item.Description, Font, descriptionBrush, e.Bounds.Left + 10, e.Bounds.Top + 23);
            }

            e.DrawFocusRectangle();
        }

        private void ExecuteSelectedOrTyped()
        {
            AdminCommandItem selected = suggestionList.SelectedItem as AdminCommandItem;
            string commandLine = selected == null ? commandTextBox.Text.Trim() : selected.Command;

            if (String.IsNullOrWhiteSpace(commandLine))
            {
                MessageBox.Show(this, "Digite ou selecione um comando.", "Comando obrigatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                commandTextBox.Focus();
                return;
            }

            try
            {
                string fileName;
                string arguments;
                SplitCommandLine(Environment.ExpandEnvironmentVariables(commandLine), out fileName, out arguments);

                bool keepConsoleOpen = selected != null
                    ? selected.KeepConsoleOpen
                    : AdminCommandCatalog.IsConsoleCommand(fileName);

                ProcessStartInfo psi = new ProcessStartInfo();
                if (keepConsoleOpen)
                {
                    psi.FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
                    psi.Arguments = "/K " + Environment.ExpandEnvironmentVariables(commandLine);
                }
                else
                {
                    psi.FileName = fileName;
                    psi.Arguments = arguments;
                }
                psi.UseShellExecute = true;
                psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!IsAdministrator())
                {
                    psi.Verb = "runas";
                }

                Process.Start(psi);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "Nao foi possivel executar o comando.\r\n\r\n" + ex.Message,
                    "Falha ao executar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                commandTextBox.Focus();
                commandTextBox.SelectAll();
            }
        }

        private static void SplitCommandLine(string commandLine, out string fileName, out string arguments)
        {
            string value = (commandLine ?? "").Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = value.IndexOf('"', 1);
                if (closingQuote < 0)
                {
                    throw new InvalidOperationException("Feche as aspas do caminho antes de executar.");
                }

                fileName = value.Substring(1, closingQuote - 1);
                arguments = value.Substring(closingQuote + 1).TrimStart();
                return;
            }

            int separator = -1;
            for (int i = 0; i < value.Length; i++)
            {
                if (Char.IsWhiteSpace(value[i]))
                {
                    separator = i;
                    break;
                }
            }

            if (separator < 0)
            {
                fileName = value;
                arguments = "";
            }
            else
            {
                fileName = value.Substring(0, separator);
                arguments = value.Substring(separator).TrimStart();
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using (System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class SolppeDialogBrand
    {
        public static Panel CreateHeader(int width, string titleText, string subtitleText)
        {
            Color darkBlue = Color.FromArgb(7, 45, 75);
            Color emerald = Color.FromArgb(8, 154, 103);
            Panel header = new Panel();
            header.SetBounds(0, 0, width, 84);
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.BackColor = Color.FromArgb(243, 248, 250);

            Panel accent = new Panel();
            accent.SetBounds(0, 0, 6, 84);
            accent.BackColor = emerald;
            header.Controls.Add(accent);

            PictureBox logo = new PictureBox();
            logo.SetBounds(18, 13, 176, 58);
            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            logo.Image = LoadBrandLogo();
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = titleText;
            title.SetBounds(210, 14, width - 230, 30);
            title.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = subtitleText;
            subtitle.SetBounds(212, 45, width - 232, 24);
            subtitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = Color.FromArgb(89, 111, 125);
            header.Controls.Add(subtitle);
            return header;
        }

        private static Image LoadBrandLogo()
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ToolkitAll.Assets.SolppeLogo.png"))
                {
                    if (stream == null) return null;
                    using (Image source = Image.FromStream(stream)) return TrimTransparentImage(new Bitmap(source));
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image TrimTransparentImage(Bitmap bitmap)
        {
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < bitmap.Height; y += 2)
            {
                for (int x = 0; x < bitmap.Width; x += 2)
                {
                    if (bitmap.GetPixel(x, y).A <= 10) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            if (right < left || bottom < top) return bitmap;

            int padding = 6;
            left = Math.Max(0, left - padding);
            top = Math.Max(0, top - padding);
            right = Math.Min(bitmap.Width - 1, right + padding);
            bottom = Math.Min(bitmap.Height - 1, bottom + padding);
            Bitmap trimmed = new Bitmap(right - left + 1, bottom - top + 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(trimmed))
            {
                graphics.DrawImage(bitmap, new Rectangle(0, 0, trimmed.Width, trimmed.Height), new Rectangle(left, top, trimmed.Width, trimmed.Height), GraphicsUnit.Pixel);
            }
            bitmap.Dispose();
            return trimmed;
        }
    }

    internal sealed class OfficeProductChoice
    {
        public readonly string Code;
        public readonly string Label;
        public readonly string Category;

        public OfficeProductChoice(string code, string label, string category)
        {
            Code = code;
            Label = label;
            Category = category;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    internal sealed class OfficeDownloadItem
    {
        private const string OfficeCdnBase = "https://officecdn.microsoft.com/db/492350f6-3a01-4f97-b9c0-c7c6ddf67d60/media/";

        public readonly int Year;
        public readonly string Code;
        public readonly string Label;
        public readonly string LanguageCode;
        public readonly string Url;

        public OfficeDownloadItem(int year, OfficeProductChoice product, string languageCode)
        {
            Year = year;
            Code = product.Code;
            Label = product.Label;
            LanguageCode = String.IsNullOrWhiteSpace(languageCode) ? "pt-br" : languageCode;
            Url = OfficeCdnBase + LanguageCode + "/" + product.Code + year + "Retail.img";
        }
    }

    internal sealed class OfficeDownloadPlan
    {
        public int Year;
        public string LanguageCode = "pt-br";
        public readonly List<OfficeDownloadItem> Items = new List<OfficeDownloadItem>();

        public string GetSummary()
        {
            if (Items.Count == 0) return "nenhum produto";
            string language = String.Equals(LanguageCode, "en-us", StringComparison.OrdinalIgnoreCase) ? "English (EUA)" : "Portugues (Brasil)";
            if (Items.Count == 1) return Items[0].Label + " " + Year + " | " + language;
            return Items.Count + " downloads do Office " + Year + " | " + language;
        }
    }

    internal sealed class OfficeDownloadDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color emerald = Color.FromArgb(8, 154, 103);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly OfficeDownloadPlan initialPlan;
        private readonly ComboBox yearComboBox = new ComboBox();
        private readonly ComboBox languageComboBox = new ComboBox();
        private readonly CheckedListBox packageList = new CheckedListBox();
        private readonly CheckedListBox applicationList = new CheckedListBox();
        private readonly Label selectionLabel = new Label();
        private bool updatingSelectionMode;

        public OfficeDownloadPlan SelectedPlan { get; private set; }

        public OfficeDownloadDialog(OfficeDownloadPlan currentPlan)
        {
            initialPlan = currentPlan;
            Text = "Instalar Office oficial";
            ClientSize = new Size(820, 680);
            MinimumSize = new Size(820, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            LoadProducts();
        }

        private void BuildLayout()
        {
            Controls.Add(SolppeDialogBrand.CreateHeader(ClientSize.Width, "Instalacao oficial do Microsoft Office", "O toolkit baixa e instala em segundo plano, sem abrir o navegador"));

            Label yearLabel = new Label();
            yearLabel.Text = "Ano / versao";
            yearLabel.SetBounds(26, 100, 150, 22);
            yearLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            yearLabel.ForeColor = blue;
            Controls.Add(yearLabel);

            yearComboBox.SetBounds(26, 124, 170, 30);
            yearComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            yearComboBox.Items.Add("Office 2019");
            yearComboBox.Items.Add("Office 2021");
            yearComboBox.SelectedIndex = initialPlan != null && initialPlan.Year == 2019 ? 0 : 1;
            yearComboBox.SelectedIndexChanged += delegate { LoadProducts(); };
            Controls.Add(yearComboBox);

            Label languageLabel = new Label();
            languageLabel.Text = "Idioma da midia";
            languageLabel.SetBounds(218, 100, 180, 22);
            languageLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            languageLabel.ForeColor = blue;
            Controls.Add(languageLabel);

            languageComboBox.SetBounds(218, 124, 236, 30);
            languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            languageComboBox.Items.Add("Portugues (Brasil) - pt-BR");
            languageComboBox.Items.Add("English (United States) - en-US");
            languageComboBox.SelectedIndex = initialPlan != null && String.Equals(initialPlan.LanguageCode, "en-us", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            languageComboBox.SelectedIndexChanged += delegate { UpdateSelectionText(); };
            Controls.Add(languageComboBox);

            Button completeButton = new Button();
            completeButton.Text = "Escolher Professional Plus";
            completeButton.SetBounds(574, 118, 220, 36);
            StyleSecondaryButton(completeButton);
            completeButton.Click += delegate { SelectOnly("ProPlus"); };
            Controls.Add(completeButton);

            Panel packagesPanel = CreateProductPanel(
                26,
                "PACOTES COMPLETOS",
                "Um pacote instala um conjunto de aplicativos do Office.",
                packageList,
                "Para a maioria das empresas, escolha Professional Plus.",
                blue);
            Controls.Add(packagesPanel);

            Panel applicationsPanel = CreateProductPanel(
                424,
                "APLICATIVOS INDIVIDUAIS",
                "Baixe somente o aplicativo ou ferramenta necessaria.",
                applicationList,
                "Exemplo: marque apenas Excel para baixar somente o Excel.",
                emerald);
            Controls.Add(applicationsPanel);

            packageList.ItemCheck += delegate
            {
                QueueSelectionModeUpdate();
            };
            applicationList.ItemCheck += delegate
            {
                QueueSelectionModeUpdate();
            };

            selectionLabel.SetBounds(28, 548, 760, 24);
            selectionLabel.ForeColor = darkBlue;
            selectionLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            Controls.Add(selectionLabel);

            Label licenseNote = new Label();
            licenseNote.Text = "A instalacao usa a Office Deployment Tool oficial. Mantenha o toolkit aberto; a ativacao exige uma licenca valida.";
            licenseNote.SetBounds(28, 574, 760, 34);
            licenseNote.ForeColor = Color.FromArgb(133, 91, 24);
            Controls.Add(licenseNote);

            Button clearButton = new Button();
            clearButton.Text = "Limpar selecao";
            clearButton.SetBounds(424, 624, 120, 38);
            StyleSecondaryButton(clearButton);
            clearButton.Click += delegate { ClearSelection(); };
            Controls.Add(clearButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.SetBounds(554, 624, 104, 38);
            cancelButton.DialogResult = DialogResult.Cancel;
            StyleSecondaryButton(cancelButton);
            Controls.Add(cancelButton);

            Button confirmButton = new Button();
            confirmButton.Text = "Baixar e instalar";
            confirmButton.SetBounds(668, 624, 126, 38);
            confirmButton.FlatStyle = FlatStyle.Flat;
            confirmButton.FlatAppearance.BorderColor = emerald;
            confirmButton.BackColor = emerald;
            confirmButton.ForeColor = Color.White;
            confirmButton.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            confirmButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(confirmButton);

            AcceptButton = confirmButton;
            CancelButton = cancelButton;
        }

        private Panel CreateProductPanel(int left, string titleText, string subtitleText, CheckedListBox list, string hintText, Color accentColor)
        {
            Panel panel = new Panel();
            panel.SetBounds(left, 174, 370, 360);
            panel.BackColor = Color.White;
            panel.BorderStyle = BorderStyle.FixedSingle;

            Panel accent = new Panel();
            accent.SetBounds(0, 0, 370, 5);
            accent.BackColor = accentColor;
            panel.Controls.Add(accent);

            Label title = new Label();
            title.Text = titleText;
            title.SetBounds(16, 16, 336, 24);
            title.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            panel.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = subtitleText;
            subtitle.SetBounds(16, 42, 336, 36);
            subtitle.ForeColor = Color.FromArgb(89, 111, 125);
            panel.Controls.Add(subtitle);

            list.SetBounds(16, 84, 336, 218);
            list.CheckOnClick = true;
            list.IntegralHeight = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            panel.Controls.Add(list);

            Label hint = new Label();
            hint.Text = hintText;
            hint.SetBounds(16, 312, 336, 36);
            hint.ForeColor = accentColor;
            hint.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            panel.Controls.Add(hint);
            return panel;
        }

        private List<OfficeProductChoice> GetCatalog()
        {
            return new List<OfficeProductChoice>
            {
                new OfficeProductChoice("ProPlus", "Office Professional Plus", "Completo"),
                new OfficeProductChoice("Professional", "Office Professional", "Completo"),
                new OfficeProductChoice("HomeBusiness", "Office Home & Business", "Completo"),
                new OfficeProductChoice("HomeStudent", "Office Home & Student", "Completo"),
                new OfficeProductChoice("Word", "Microsoft Word", "Aplicativo"),
                new OfficeProductChoice("Excel", "Microsoft Excel", "Aplicativo"),
                new OfficeProductChoice("PowerPoint", "Microsoft PowerPoint", "Aplicativo"),
                new OfficeProductChoice("Outlook", "Microsoft Outlook", "Aplicativo"),
                new OfficeProductChoice("Access", "Microsoft Access", "Aplicativo"),
                new OfficeProductChoice("Publisher", "Microsoft Publisher", "Aplicativo"),
                new OfficeProductChoice("ProjectPro", "Microsoft Project Professional", "Projeto"),
                new OfficeProductChoice("ProjectStd", "Microsoft Project Standard", "Projeto"),
                new OfficeProductChoice("VisioPro", "Microsoft Visio Professional", "Diagrama"),
                new OfficeProductChoice("VisioStd", "Microsoft Visio Standard", "Diagrama")
            };
        }

        private void LoadProducts()
        {
            if (yearComboBox.SelectedIndex < 0) return;
            int year = yearComboBox.SelectedIndex == 0 ? 2019 : 2021;
            HashSet<string> selectedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (initialPlan != null && initialPlan.Year == year)
            {
                for (int i = 0; i < initialPlan.Items.Count; i++) selectedCodes.Add(initialPlan.Items[i].Code);
            }

            packageList.Items.Clear();
            applicationList.Items.Clear();
            List<OfficeProductChoice> catalog = GetCatalog();
            for (int i = 0; i < catalog.Count; i++)
            {
                bool check = selectedCodes.Count > 0 ? selectedCodes.Contains(catalog[i].Code) : String.Equals(catalog[i].Code, "ProPlus", StringComparison.OrdinalIgnoreCase);
                if (String.Equals(catalog[i].Category, "Completo", StringComparison.OrdinalIgnoreCase))
                {
                    packageList.Items.Add(catalog[i], check);
                }
                else
                {
                    applicationList.Items.Add(catalog[i], check);
                }
            }
            UpdateSelectionMode();
        }

        private void SelectOnly(string code)
        {
            updatingSelectionMode = true;
            try
            {
                SelectOnlyInList(packageList, code);
                SelectOnlyInList(applicationList, code);
            }
            finally
            {
                updatingSelectionMode = false;
            }
            UpdateSelectionMode();
        }

        private void SelectOnlyInList(CheckedListBox list, string code)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                OfficeProductChoice product = list.Items[i] as OfficeProductChoice;
                list.SetItemChecked(i, product != null && String.Equals(product.Code, code, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ClearSelection()
        {
            updatingSelectionMode = true;
            try
            {
                ClearCheckedItems(packageList);
                ClearCheckedItems(applicationList);
            }
            finally
            {
                updatingSelectionMode = false;
            }
            UpdateSelectionMode();
        }

        private void QueueSelectionModeUpdate()
        {
            if (updatingSelectionMode) return;
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(UpdateSelectionMode));
            }
        }

        private void UpdateSelectionMode()
        {
            if (updatingSelectionMode) return;
            updatingSelectionMode = true;
            try
            {
                int packages = packageList.CheckedItems.Count;
                int applications = applicationList.CheckedItems.Count;
                if (packages > 0)
                {
                    ClearCheckedItems(applicationList);
                    packageList.Enabled = true;
                    applicationList.Enabled = false;
                }
                else if (applications > 0)
                {
                    ClearCheckedItems(packageList);
                    packageList.Enabled = false;
                    applicationList.Enabled = true;
                }
                else
                {
                    packageList.Enabled = true;
                    applicationList.Enabled = true;
                }
            }
            finally
            {
                updatingSelectionMode = false;
            }
            UpdateSelectionText();
        }

        private void ClearCheckedItems(CheckedListBox list)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                if (list.GetItemChecked(i)) list.SetItemChecked(i, false);
            }
        }

        private void UpdateSelectionText()
        {
            int packages = packageList.CheckedItems.Count;
            int applications = applicationList.CheckedItems.Count;
            int count = packages + applications;
            string language = languageComboBox.SelectedIndex == 1 ? "English (EUA)" : "Portugues (Brasil)";
            string countText = count == 1 ? "1 produto selecionado" : count + " produtos selecionados";
            selectionLabel.Text = countText + "  |  " + packages + " pacote(s)  |  " + applications + " aplicativo(s)  |  " + language;
        }

        private void ConfirmSelection()
        {
            if (packageList.CheckedItems.Count + applicationList.CheckedItems.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos uma edicao ou aplicativo.", "Produto obrigatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int year = yearComboBox.SelectedIndex == 0 ? 2019 : 2021;
            OfficeDownloadPlan plan = new OfficeDownloadPlan();
            plan.Year = year;
            plan.LanguageCode = languageComboBox.SelectedIndex == 1 ? "en-us" : "pt-br";
            AddCheckedItemsToPlan(packageList, plan);
            AddCheckedItemsToPlan(applicationList, plan);
            SelectedPlan = plan;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void AddCheckedItemsToPlan(CheckedListBox list, OfficeDownloadPlan plan)
        {
            for (int i = 0; i < list.CheckedItems.Count; i++)
            {
                OfficeProductChoice product = list.CheckedItems[i] as OfficeProductChoice;
                if (product != null) plan.Items.Add(new OfficeDownloadItem(plan.Year, product, plan.LanguageCode));
            }
        }

        private void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.BackColor = Color.White;
            button.ForeColor = darkBlue;
        }
    }

    internal sealed class LicenseOfficeOption
    {
        public readonly string Id;
        public readonly string Kind;
        public readonly string Label;
        public readonly string Description;
        public readonly string Target;
        public readonly string ProgressText;
        public readonly string LogText;

        public LicenseOfficeOption(string id, string kind, string label, string description, string target, string progressText, string logText)
        {
            Id = id;
            Kind = kind;
            Label = label;
            Description = description;
            Target = target;
            ProgressText = progressText;
            LogText = logText;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    internal sealed class LicenseOfficeDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color emerald = Color.FromArgb(8, 154, 103);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly ListBox optionList = new ListBox();
        private readonly Label descriptionLabel = new Label();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();

        public LicenseOfficeOption SelectedOption { get; private set; }

        public LicenseOfficeDialog(LicenseOfficeOption currentOption)
        {
            Text = "Licencas e Office";
            ClientSize = new Size(660, 430);
            MinimumSize = new Size(660, 430);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            LoadOptions(currentOption);
        }

        private void BuildLayout()
        {
            Controls.Add(SolppeDialogBrand.CreateHeader(ClientSize.Width, "Licencas e Office", "Escolha uma acao de suporte, ativacao ou download oficial"));

            optionList.Left = 28;
            optionList.Top = 104;
            optionList.Width = 604;
            optionList.Height = 176;
            optionList.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            optionList.BorderStyle = BorderStyle.FixedSingle;
            optionList.SelectedIndexChanged += delegate { UpdateDescription(); };
            optionList.DoubleClick += delegate { ConfirmSelection(); };
            Controls.Add(optionList);

            descriptionLabel.Left = 28;
            descriptionLabel.Top = 294;
            descriptionLabel.Width = 604;
            descriptionLabel.Height = 56;
            descriptionLabel.Padding = new Padding(14, 10, 12, 8);
            descriptionLabel.BackColor = Color.FromArgb(243, 248, 250);
            descriptionLabel.ForeColor = blue;
            Controls.Add(descriptionLabel);

            Panel descriptionAccent = new Panel();
            descriptionAccent.SetBounds(28, 294, 5, 56);
            descriptionAccent.BackColor = emerald;
            Controls.Add(descriptionAccent);
            descriptionAccent.BringToFront();

            okButton.Text = "Salvar";
            okButton.Left = 426;
            okButton.Top = 370;
            okButton.Width = 98;
            okButton.Height = 38;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = emerald;
            okButton.BackColor = emerald;
            okButton.ForeColor = Color.White;
            okButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            okButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(okButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 534;
            cancelButton.Top = 370;
            cancelButton.Width = 98;
            cancelButton.Height = 38;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadOptions(LicenseOfficeOption currentOption)
        {
            LicenseOfficeOption[] options = new LicenseOfficeOption[]
            {
                new LicenseOfficeOption("windows-settings", "settings", "Abrir ativacao do Windows", "Abre diretamente as Configuracoes do Windows na tela de ativacao.", "ms-settings:activation", "Abrindo ativacao do Windows...", "Abrindo Configuracoes > Ativacao."),
                new LicenseOfficeOption("crystal-installer", "admin-powershell", "MasGrave", "Abre o PowerShell como administrador, executa o instalador interligado e mantem a janela aberta para acompanhar o resultado.", "irm https://get.activated.win | iex", "Abrindo instalador Crystal / TekFarma...", "Abrindo PowerShell administrativo com o instalador interligado."),
                new LicenseOfficeOption("windows-help", "url", "Ajuda oficial de ativacao Windows", "Abre a pagina oficial da Microsoft sobre ativacao com chave de produto ou licenca digital.", "https://support.microsoft.com/windows/activate-windows-c39005d4-95ee-b91e-b399-2820fda32227", "Abrindo ajuda Windows...", "Abrindo ajuda oficial de ativacao Windows."),
                new LicenseOfficeOption("office-help", "url", "Ajuda oficial de ativacao Office", "Abre a pagina oficial da Microsoft para ativar Microsoft 365 ou Office.", "https://support.microsoft.com/office/activate-office-5bd38f38-db92-448b-a982-ad170b1e187e", "Abrindo ajuda Office...", "Abrindo ajuda oficial de ativacao Office."),
                new LicenseOfficeOption("office-download", "office-download-selector", "Instalar Office (pacote ou aplicativo)", "Seleciona Office 2019 ou 2021 e instala silenciosamente um pacote completo ou aplicativos individuais pelo CDN oficial da Microsoft.", "", "Baixando e instalando o Office...", "Preparando instalacao silenciosa pela Office Deployment Tool oficial.")
            };

            optionList.Items.AddRange(options);

            int selectedIndex = 0;
            if (currentOption != null)
            {
                for (int i = 0; i < optionList.Items.Count; i++)
                {
                    LicenseOfficeOption option = optionList.Items[i] as LicenseOfficeOption;
                    if (option != null && String.Equals(option.Id, currentOption.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            optionList.SelectedIndex = selectedIndex;
            UpdateDescription();
        }

        private void UpdateDescription()
        {
            LicenseOfficeOption option = optionList.SelectedItem as LicenseOfficeOption;
            descriptionLabel.Text = option == null ? "" : option.Description;
        }

        private void ConfirmSelection()
        {
            LicenseOfficeOption option = optionList.SelectedItem as LicenseOfficeOption;

            if (option == null)
            {
                MessageBox.Show(this, "Selecione uma opcao.", "Opcao obrigatoria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedOption = option;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class MappingHostDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly TextBox hostTextBox = new TextBox();

        public string SelectedHost { get; private set; }

        public MappingHostDialog(string currentHost)
        {
            Text = "Configurar mapeamento";
            ClientSize = new Size(520, 224);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            Label title = new Label();
            title.Text = "Mapear sistema";
            title.SetBounds(24, 20, 460, 30);
            title.Font = new Font("Segoe UI", 15F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            Label label = new Label();
            label.Text = "Host do servidor";
            label.SetBounds(24, 64, 180, 22);
            label.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = blue;
            Controls.Add(label);

            hostTextBox.SetBounds(24, 92, 176, 28);
            hostTextBox.CharacterCasing = CharacterCasing.Upper;
            hostTextBox.Text = String.IsNullOrWhiteSpace(currentHost) ? "SERVIDOR" : currentHost;
            Controls.Add(hostTextBox);

            AddPresetButton("SERVIDOR", 216);
            AddPresetButton("SERVER", 316);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.SetBounds(300, 164, 92, 36);
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            Button okButton = new Button();
            okButton.Text = "Confirmar";
            okButton.SetBounds(404, 164, 92, 36);
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = border;
            okButton.BackColor = Color.White;
            okButton.ForeColor = Color.FromArgb(18, 24, 32);
            okButton.Click += ConfirmSelection;
            Controls.Add(okButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
            Shown += delegate
            {
                hostTextBox.Focus();
                hostTextBox.SelectAll();
            };
        }

        private void AddPresetButton(string text, int left)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(left, 90, text.Length > 6 ? 88 : 72, 30);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.BackColor = Color.White;
            button.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            button.Click += delegate { hostTextBox.Text = text; };
            Controls.Add(button);
        }

        private void ConfirmSelection(object sender, EventArgs e)
        {
            string host = hostTextBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(host))
            {
                MessageBox.Show(this, "Informe o host do servidor.", "Mapear sistema", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                hostTextBox.Focus();
                return;
            }

            SelectedHost = host;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class SefazTimeZoneOption
    {
        public readonly string Label;
        public readonly string TimeZoneId;

        public SefazTimeZoneOption(string label, string timeZoneId)
        {
            Label = label;
            TimeZoneId = timeZoneId;
        }

        public override string ToString()
        {
            return Label;
        }
    }

    internal sealed class NetworkAdapterInfo
    {
        public int InterfaceIndex;
        public string Name;
        public string Description;
        public string Status;
        public bool DhcpEnabled;
        public string IpAddress;
        public int PrefixLength;
        public string Gateway;
        public string[] DnsServers;
        public bool AutomaticDns;

        public override string ToString()
        {
            string state = String.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase) ? "Conectado" : Status;
            return Name + "  |  " + state + "  |  " + Description;
        }
    }

    internal sealed class NetworkConfigurationPlan
    {
        public int InterfaceIndex;
        public string InterfaceAlias;
        public bool ApplyIpSettings;
        public bool UseDhcp;
        public string IpAddress;
        public int PrefixLength;
        public string Gateway;
        public bool ApplyDnsSettings;
        public bool UseAutomaticDns;
        public string PrimaryDns;
        public string SecondaryDns;
        public bool FlushAndRenewDns;
        public bool ResetWinHttpProxy;
        public bool EnableTls12;
        public bool ResetWinsockAndTcpIp;
        public bool OpenInternetAdvancedOptions;
        public bool TestConnectivity;

        public string GetSummary()
        {
            List<string> items = new List<string>();
            items.Add(InterfaceAlias);
            if (ApplyIpSettings) items.Add(UseDhcp ? "IPv4 DHCP" : "IPv4 " + IpAddress + "/" + PrefixLength);
            if (ApplyDnsSettings) items.Add(UseAutomaticDns ? "DNS automatico" : "DNS " + PrimaryDns);
            if (ResetWinsockAndTcpIp) items.Add("reset TCP/IP");
            if (EnableTls12) items.Add("TLS 1.2");
            return String.Join(" | ", items.ToArray());
        }
    }

    internal enum IpAvailabilityState
    {
        CurrentAddress,
        Available,
        InUse,
        Inconclusive
    }

    internal sealed class NetworkConfigurationDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color emerald = Color.FromArgb(8, 154, 103);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly NetworkConfigurationPlan initialPlan;
        private readonly ComboBox adapterComboBox = new ComboBox();
        private readonly Label adapterDetailsLabel = new Label();
        private readonly Button refreshButton = new Button();
        private readonly CheckBox applyIpCheckBox = new CheckBox();
        private readonly RadioButton dhcpRadio = new RadioButton();
        private readonly RadioButton staticIpRadio = new RadioButton();
        private readonly TextBox ipAddressTextBox = new TextBox();
        private readonly TextBox prefixLengthTextBox = new TextBox();
        private readonly TextBox gatewayTextBox = new TextBox();
        private readonly Button testIpButton = new Button();
        private readonly CheckBox applyDnsCheckBox = new CheckBox();
        private readonly RadioButton automaticDnsRadio = new RadioButton();
        private readonly RadioButton manualDnsRadio = new RadioButton();
        private readonly TextBox primaryDnsTextBox = new TextBox();
        private readonly TextBox secondaryDnsTextBox = new TextBox();
        private readonly CheckBox flushDnsCheckBox = new CheckBox();
        private readonly CheckBox resetProxyCheckBox = new CheckBox();
        private readonly CheckBox enableTlsCheckBox = new CheckBox();
        private readonly CheckBox resetStackCheckBox = new CheckBox();
        private readonly CheckBox openInternetOptionsCheckBox = new CheckBox();
        private readonly CheckBox testConnectivityCheckBox = new CheckBox();
        private readonly Button saveButton = new Button();
        private bool loadingAdapters;

        public NetworkConfigurationPlan SelectedPlan { get; private set; }

        public NetworkConfigurationDialog(NetworkConfigurationPlan currentPlan)
        {
            initialPlan = currentPlan;
            Text = "Configuracao avancada de rede";
            ClientSize = new Size(800, 680);
            MinimumSize = new Size(800, 680);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            Shown += delegate { LoadAdapters(); };
        }

        private void BuildLayout()
        {
            Label title = new Label();
            title.Text = "IP, DNS e reparo de conexao";
            title.SetBounds(24, 16, 520, 34);
            title.Font = new Font("Segoe UI Semibold", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Escolha o adaptador antes de alterar a configuracao. As mudancas sao aplicadas somente ao clicar em Executar no toolkit.";
            subtitle.SetBounds(26, 52, 740, 30);
            subtitle.ForeColor = Color.FromArgb(89, 111, 125);
            Controls.Add(subtitle);

            Label adapterLabel = new Label();
            adapterLabel.Text = "Adaptador de rede";
            adapterLabel.SetBounds(26, 88, 180, 22);
            adapterLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            adapterLabel.ForeColor = blue;
            Controls.Add(adapterLabel);

            adapterComboBox.SetBounds(26, 112, 620, 30);
            adapterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            adapterComboBox.SelectedIndexChanged += delegate { LoadSelectedAdapter(); };
            Controls.Add(adapterComboBox);

            refreshButton.Text = "Atualizar";
            refreshButton.SetBounds(656, 110, 116, 32);
            StyleSecondaryButton(refreshButton);
            refreshButton.Click += delegate { LoadAdapters(); };
            Controls.Add(refreshButton);

            adapterDetailsLabel.SetBounds(28, 145, 740, 24);
            adapterDetailsLabel.ForeColor = Color.FromArgb(89, 111, 125);
            Controls.Add(adapterDetailsLabel);

            TabControl tabs = new TabControl();
            tabs.SetBounds(24, 176, 748, 420);
            tabs.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            Controls.Add(tabs);

            TabPage ipPage = new TabPage("IPv4 e DNS");
            ipPage.BackColor = Color.White;
            tabs.TabPages.Add(ipPage);
            BuildIpPage(ipPage);

            TabPage repairPage = new TabPage("Reparo e Internet");
            repairPage.BackColor = Color.White;
            tabs.TabPages.Add(repairPage);
            BuildRepairPage(repairPage);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancelar";
            cancelButton.SetBounds(518, 620, 104, 38);
            cancelButton.DialogResult = DialogResult.Cancel;
            StyleSecondaryButton(cancelButton);
            Controls.Add(cancelButton);

            saveButton.Text = "Salvar configuracao";
            saveButton.SetBounds(632, 620, 140, 38);
            saveButton.FlatStyle = FlatStyle.Flat;
            saveButton.FlatAppearance.BorderColor = emerald;
            saveButton.BackColor = emerald;
            saveButton.ForeColor = Color.White;
            saveButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            saveButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(saveButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        private void BuildIpPage(Control page)
        {
            applyIpCheckBox.Text = "Aplicar configuracao IPv4 neste adaptador";
            applyIpCheckBox.SetBounds(20, 18, 330, 24);
            applyIpCheckBox.Checked = true;
            applyIpCheckBox.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            applyIpCheckBox.ForeColor = darkBlue;
            applyIpCheckBox.CheckedChanged += delegate { UpdateFieldState(); };
            page.Controls.Add(applyIpCheckBox);

            Panel ipModePanel = new Panel();
            ipModePanel.SetBounds(32, 46, 390, 66);
            ipModePanel.BackColor = Color.White;
            page.Controls.Add(ipModePanel);

            dhcpRadio.Text = "Obter endereco IP automaticamente (DHCP)";
            dhcpRadio.SetBounds(8, 4, 330, 24);
            dhcpRadio.CheckedChanged += delegate { UpdateFieldState(); };
            ipModePanel.Controls.Add(dhcpRadio);

            staticIpRadio.Text = "Usar endereco IPv4 fixo";
            staticIpRadio.SetBounds(8, 34, 240, 24);
            staticIpRadio.CheckedChanged += delegate { UpdateFieldState(); };
            ipModePanel.Controls.Add(staticIpRadio);

            AddField(page, "Endereco IP", ipAddressTextBox, 58, 116, 206);
            AddField(page, "Prefixo (ex.: 24)", prefixLengthTextBox, 282, 116, 118);
            AddField(page, "Gateway", gatewayTextBox, 418, 116, 206);

            testIpButton.Text = "Testar IP";
            testIpButton.SetBounds(632, 138, 78, 28);
            StyleSecondaryButton(testIpButton);
            testIpButton.Click += delegate { TestIpFromDialog(); };
            page.Controls.Add(testIpButton);

            Button automaticButton = new Button();
            automaticButton.Text = "Restaurar DHCP + DNS automatico";
            automaticButton.SetBounds(476, 18, 234, 34);
            StyleSecondaryButton(automaticButton);
            automaticButton.Click += delegate
            {
                applyIpCheckBox.Checked = true;
                dhcpRadio.Checked = true;
                applyDnsCheckBox.Checked = true;
                automaticDnsRadio.Checked = true;
                UpdateFieldState();
            };
            page.Controls.Add(automaticButton);

            Panel divider = new Panel();
            divider.SetBounds(20, 176, 690, 1);
            divider.BackColor = border;
            page.Controls.Add(divider);

            applyDnsCheckBox.Text = "Aplicar configuracao DNS neste adaptador";
            applyDnsCheckBox.SetBounds(20, 194, 330, 24);
            applyDnsCheckBox.Checked = true;
            applyDnsCheckBox.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point);
            applyDnsCheckBox.ForeColor = darkBlue;
            applyDnsCheckBox.CheckedChanged += delegate { UpdateFieldState(); };
            page.Controls.Add(applyDnsCheckBox);

            Panel dnsModePanel = new Panel();
            dnsModePanel.SetBounds(32, 222, 390, 66);
            dnsModePanel.BackColor = Color.White;
            page.Controls.Add(dnsModePanel);

            automaticDnsRadio.Text = "Obter servidores DNS automaticamente";
            automaticDnsRadio.SetBounds(8, 4, 320, 24);
            automaticDnsRadio.CheckedChanged += delegate { UpdateFieldState(); };
            dnsModePanel.Controls.Add(automaticDnsRadio);

            manualDnsRadio.Text = "Usar servidores DNS informados";
            manualDnsRadio.SetBounds(8, 34, 280, 24);
            manualDnsRadio.CheckedChanged += delegate { UpdateFieldState(); };
            dnsModePanel.Controls.Add(manualDnsRadio);

            AddField(page, "DNS preferencial", primaryDnsTextBox, 58, 292, 206);
            AddField(page, "DNS alternativo", secondaryDnsTextBox, 282, 292, 206);

            Label note = new Label();
            note.Text = "O IP fixo substitui os enderecos IPv4 atuais do adaptador selecionado.\r\nAntes de aplicar, o toolkit verifica Ping e ARP para evitar endereco duplicado.";
            note.SetBounds(40, 344, 650, 42);
            note.ForeColor = Color.FromArgb(133, 91, 24);
            page.Controls.Add(note);
        }

        private void BuildRepairPage(Control page)
        {
            Label info = new Label();
            info.Text = "Selecione somente os reparos necessarios. O reset de Winsock/TCP-IP afeta todos os adaptadores e pode exigir reinicializacao.";
            info.SetBounds(20, 18, 690, 44);
            info.ForeColor = Color.FromArgb(89, 111, 125);
            page.Controls.Add(info);

            flushDnsCheckBox.Text = "Limpar e registrar DNS; renovar DHCP do adaptador selecionado";
            flushDnsCheckBox.SetBounds(32, 78, 600, 26);
            flushDnsCheckBox.Checked = true;
            page.Controls.Add(flushDnsCheckBox);

            resetProxyCheckBox.Text = "Restaurar proxy WinHTTP para acesso direto";
            resetProxyCheckBox.SetBounds(32, 112, 520, 26);
            resetProxyCheckBox.Checked = true;
            page.Controls.Add(resetProxyCheckBox);

            enableTlsCheckBox.Text = "Restaurar padroes seguros de TLS 1.2 (.NET, WinHTTP e SChannel)";
            enableTlsCheckBox.SetBounds(32, 146, 600, 26);
            enableTlsCheckBox.Checked = true;
            page.Controls.Add(enableTlsCheckBox);

            resetStackCheckBox.Text = "Resetar Winsock e pilha TCP/IP do Windows (requer reinicializacao)";
            resetStackCheckBox.SetBounds(32, 180, 610, 26);
            page.Controls.Add(resetStackCheckBox);

            openInternetOptionsCheckBox.Text = "Abrir a guia Avancadas do inetcpl.cpl para restaurar as opcoes da Internet";
            openInternetOptionsCheckBox.SetBounds(32, 214, 650, 26);
            page.Controls.Add(openInternetOptionsCheckBox);

            testConnectivityCheckBox.Text = "Testar resolucao DNS e conexao HTTPS/TLS ao concluir";
            testConnectivityCheckBox.SetBounds(32, 248, 560, 26);
            testConnectivityCheckBox.Checked = true;
            page.Controls.Add(testConnectivityCheckBox);

            Button openNowButton = new Button();
            openNowButton.Text = "Abrir Opcoes da Internet agora";
            openNowButton.SetBounds(32, 294, 238, 36);
            StyleSecondaryButton(openNowButton);
            openNowButton.Click += delegate { OpenInternetAdvancedOptions(); };
            page.Controls.Add(openNowButton);

            Label restoreNote = new Label();
            restoreNote.Text = "Na guia Avancadas, clique em 'Restaurar configuracoes avancadas'. Essa confirmacao permanece visivel para evitar alterar opcoes pessoais sem autorizacao.";
            restoreNote.SetBounds(32, 338, 650, 44);
            restoreNote.ForeColor = blue;
            page.Controls.Add(restoreNote);
        }

        private void AddField(Control parent, string labelText, TextBox textBox, int left, int top, int width)
        {
            Label label = new Label();
            label.Text = labelText;
            label.SetBounds(left, top, width, 20);
            label.ForeColor = Color.FromArgb(89, 111, 125);
            parent.Controls.Add(label);

            textBox.SetBounds(left, top + 22, width, 28);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            parent.Controls.Add(textBox);
        }

        private void StyleSecondaryButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.BackColor = Color.White;
            button.ForeColor = darkBlue;
        }

        private void LoadAdapters()
        {
            if (loadingAdapters) return;
            loadingAdapters = true;
            refreshButton.Enabled = false;
            saveButton.Enabled = false;
            adapterDetailsLabel.Text = "Consultando adaptadores e configuracoes atuais...";
            UseWaitCursor = true;

            try
            {
                int wantedIndex = initialPlan == null ? -1 : initialPlan.InterfaceIndex;
                adapterComboBox.Items.Clear();
                string[] lines = RunPowerShellLines(BuildAdapterQuery());
                int selectedIndex = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(new char[] { '\t' });
                    int index;
                    int prefix;
                    if (parts.Length < 10 || !Int32.TryParse(parts[0], out index)) continue;
                    Int32.TryParse(parts[6], out prefix);

                    NetworkAdapterInfo adapter = new NetworkAdapterInfo();
                    adapter.InterfaceIndex = index;
                    adapter.Name = parts[1];
                    adapter.Description = parts[2];
                    adapter.Status = parts[3];
                    adapter.DhcpEnabled = String.Equals(parts[4], "Enabled", StringComparison.OrdinalIgnoreCase);
                    adapter.IpAddress = parts[5];
                    adapter.PrefixLength = prefix > 0 ? prefix : 24;
                    adapter.Gateway = parts[7] == "0.0.0.0" ? "" : parts[7];
                    adapter.DnsServers = String.IsNullOrWhiteSpace(parts[8]) ? new string[0] : parts[8].Split(',');
                    adapter.AutomaticDns = String.Equals(parts[9], "Automatic", StringComparison.OrdinalIgnoreCase);
                    adapterComboBox.Items.Add(adapter);
                    if (wantedIndex == index) selectedIndex = adapterComboBox.Items.Count - 1;
                }

                if (adapterComboBox.Items.Count == 0)
                {
                    adapterDetailsLabel.Text = "Nenhum adaptador de rede foi encontrado.";
                    return;
                }

                adapterComboBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
                saveButton.Enabled = true;
            }
            catch (Exception ex)
            {
                adapterDetailsLabel.Text = "Falha ao consultar adaptadores: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Erro ao consultar rede", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                UseWaitCursor = false;
                refreshButton.Enabled = true;
                loadingAdapters = false;
            }

            if (adapterComboBox.SelectedItem != null)
            {
                LoadSelectedAdapter();
                NetworkAdapterInfo selectedAdapter = adapterComboBox.SelectedItem as NetworkAdapterInfo;
                if (initialPlan != null && selectedAdapter != null && selectedAdapter.InterfaceIndex == initialPlan.InterfaceIndex)
                {
                    ApplyInitialPlan();
                }
            }
        }

        private string BuildAdapterQuery()
        {
            return
                "$ErrorActionPreference='SilentlyContinue'; " +
                "Get-NetAdapter | Sort-Object @{Expression={if($_.Status -eq 'Up'){0}else{1}}},Name | ForEach-Object { " +
                "$adapter=$_; $idx=[int]$adapter.ifIndex; " +
                "$ipif=Get-NetIPInterface -InterfaceIndex $idx -AddressFamily IPv4 | Select-Object -First 1; " +
                "$ip=Get-NetIPAddress -InterfaceIndex $idx -AddressFamily IPv4 | Where-Object {$_.IPAddress -notlike '169.254.*'} | Select-Object -First 1; " +
                "$route=Get-NetRoute -InterfaceIndex $idx -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1; " +
                "$dnsRaw=(Get-DnsClientServerAddress -InterfaceIndex $idx -AddressFamily IPv4).ServerAddresses; " +
                "$dns=(@($dnsRaw) | Where-Object {$_ -match '^\\d{1,3}(\\.\\d{1,3}){3}$'}) -join ','; " +
                "$guid=$adapter.InterfaceGuid.ToString('B'); $reg='HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\'+$guid; " +
                "$manual=(Get-ItemProperty -Path $reg -Name NameServer).NameServer; $dnsMode=if([string]::IsNullOrWhiteSpace([string]$manual)){'Automatic'}else{'Manual'}; " +
                "$values=@($idx,[string]$adapter.Name,[string]$adapter.InterfaceDescription,[string]$adapter.Status,[string]$ipif.Dhcp,[string]$ip.IPAddress,[string]$ip.PrefixLength,[string]$route.NextHop,[string]$dns,$dnsMode); " +
                "Write-Output ($values -join [char]9) }";
        }

        private string[] RunPowerShellLines(string command)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encoded;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process process = Process.Start(psi))
            {
                System.Threading.Tasks.Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                System.Threading.Tasks.Task<string> errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(20000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("A consulta de adaptadores excedeu 20 segundos.");
                }
                process.WaitForExit();
                string output = outputTask.Result;
                string error = errorTask.Result;
                if (process.ExitCode != 0 && !String.IsNullOrWhiteSpace(error))
                {
                    throw new InvalidOperationException(error.Trim());
                }
                return output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private void LoadSelectedAdapter()
        {
            if (loadingAdapters) return;
            NetworkAdapterInfo adapter = adapterComboBox.SelectedItem as NetworkAdapterInfo;
            if (adapter == null) return;

            adapterDetailsLabel.Text = "Indice " + adapter.InterfaceIndex + " | " + adapter.Status + " | IPv4 " + (String.IsNullOrWhiteSpace(adapter.IpAddress) ? "nao atribuido" : adapter.IpAddress + "/" + adapter.PrefixLength);
            dhcpRadio.Checked = adapter.DhcpEnabled;
            staticIpRadio.Checked = !adapter.DhcpEnabled;
            ipAddressTextBox.Text = adapter.IpAddress;
            prefixLengthTextBox.Text = adapter.PrefixLength.ToString();
            gatewayTextBox.Text = adapter.Gateway;
            automaticDnsRadio.Checked = adapter.AutomaticDns;
            manualDnsRadio.Checked = !adapter.AutomaticDns;
            primaryDnsTextBox.Text = adapter.DnsServers.Length > 0 ? adapter.DnsServers[0] : "";
            secondaryDnsTextBox.Text = adapter.DnsServers.Length > 1 ? adapter.DnsServers[1] : "";
            UpdateFieldState();
        }

        private void ApplyInitialPlan()
        {
            applyIpCheckBox.Checked = initialPlan.ApplyIpSettings;
            dhcpRadio.Checked = initialPlan.UseDhcp;
            staticIpRadio.Checked = !initialPlan.UseDhcp;
            ipAddressTextBox.Text = initialPlan.IpAddress;
            prefixLengthTextBox.Text = initialPlan.PrefixLength.ToString();
            gatewayTextBox.Text = initialPlan.Gateway;
            applyDnsCheckBox.Checked = initialPlan.ApplyDnsSettings;
            automaticDnsRadio.Checked = initialPlan.UseAutomaticDns;
            manualDnsRadio.Checked = !initialPlan.UseAutomaticDns;
            primaryDnsTextBox.Text = initialPlan.PrimaryDns;
            secondaryDnsTextBox.Text = initialPlan.SecondaryDns;
            flushDnsCheckBox.Checked = initialPlan.FlushAndRenewDns;
            resetProxyCheckBox.Checked = initialPlan.ResetWinHttpProxy;
            enableTlsCheckBox.Checked = initialPlan.EnableTls12;
            resetStackCheckBox.Checked = initialPlan.ResetWinsockAndTcpIp;
            openInternetOptionsCheckBox.Checked = initialPlan.OpenInternetAdvancedOptions;
            testConnectivityCheckBox.Checked = initialPlan.TestConnectivity;
            UpdateFieldState();
        }

        private void UpdateFieldState()
        {
            bool staticEnabled = applyIpCheckBox.Checked && staticIpRadio.Checked;
            dhcpRadio.Enabled = applyIpCheckBox.Checked;
            staticIpRadio.Enabled = applyIpCheckBox.Checked;
            ipAddressTextBox.Enabled = staticEnabled;
            prefixLengthTextBox.Enabled = staticEnabled;
            gatewayTextBox.Enabled = staticEnabled;
            testIpButton.Enabled = staticEnabled;

            bool manualDnsEnabled = applyDnsCheckBox.Checked && manualDnsRadio.Checked;
            automaticDnsRadio.Enabled = applyDnsCheckBox.Checked;
            manualDnsRadio.Enabled = applyDnsCheckBox.Checked;
            primaryDnsTextBox.Enabled = manualDnsEnabled;
            secondaryDnsTextBox.Enabled = manualDnsEnabled;
        }

        private void ConfirmSelection()
        {
            NetworkAdapterInfo adapter = adapterComboBox.SelectedItem as NetworkAdapterInfo;
            if (adapter == null)
            {
                MessageBox.Show(this, "Selecione um adaptador de rede.", "Adaptador obrigatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int prefix = 24;
            IpAvailabilityState ipAvailability = IpAvailabilityState.Available;
            if (applyIpCheckBox.Checked && staticIpRadio.Checked)
            {
                if (!IsValidIpv4(ipAddressTextBox.Text))
                {
                    MessageBox.Show(this, "Informe um endereco IPv4 valido.", "IPv4 invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!Int32.TryParse(prefixLengthTextBox.Text.Trim(), out prefix) || prefix < 1 || prefix > 32)
                {
                    MessageBox.Show(this, "O prefixo deve estar entre 1 e 32.", "Prefixo invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!String.IsNullOrWhiteSpace(gatewayTextBox.Text) && !IsValidIpv4(gatewayTextBox.Text))
                {
                    MessageBox.Show(this, "Informe um gateway IPv4 valido ou deixe o campo vazio.", "Gateway invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                ipAvailability = CheckIpAvailability(adapter, ipAddressTextBox.Text.Trim());
                if (ipAvailability == IpAvailabilityState.InUse)
                {
                    MessageBox.Show(
                        this,
                        "O IPv4 " + ipAddressTextBox.Text.Trim() + " respondeu ao Ping ou apareceu na tabela ARP e provavelmente ja esta em uso.\r\n\r\nNenhuma configuracao foi salva. Escolha outro endereco para evitar conflito na rede.",
                        "IPv4 ja esta em uso",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                if (ipAvailability == IpAvailabilityState.Inconclusive)
                {
                    DialogResult continueAnswer = MessageBox.Show(
                        this,
                        "Nao foi possivel concluir a verificacao de conflito deste IPv4. Deseja salvar a configuracao mesmo assim? Uma nova verificacao sera feita antes da aplicacao.",
                        "Verificacao inconclusiva",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (continueAnswer != DialogResult.Yes) return;
                }
            }

            if (applyDnsCheckBox.Checked && manualDnsRadio.Checked)
            {
                if (!IsValidIpv4(primaryDnsTextBox.Text))
                {
                    MessageBox.Show(this, "Informe um servidor DNS preferencial valido.", "DNS invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!String.IsNullOrWhiteSpace(secondaryDnsTextBox.Text) && !IsValidIpv4(secondaryDnsTextBox.Text))
                {
                    MessageBox.Show(this, "Informe um DNS alternativo valido ou deixe o campo vazio.", "DNS invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            bool hasAction = applyIpCheckBox.Checked || applyDnsCheckBox.Checked || flushDnsCheckBox.Checked ||
                resetProxyCheckBox.Checked || enableTlsCheckBox.Checked || resetStackCheckBox.Checked ||
                openInternetOptionsCheckBox.Checked || testConnectivityCheckBox.Checked;
            if (!hasAction)
            {
                MessageBox.Show(this, "Selecione ao menos uma configuracao ou reparo.", "Nenhuma acao selecionada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (resetStackCheckBox.Checked)
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "O reset de Winsock e TCP/IP afeta todos os adaptadores e exige reinicializacao. Deseja manter essa opcao?",
                    "Confirmar reset completo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            SelectedPlan = new NetworkConfigurationPlan();
            SelectedPlan.InterfaceIndex = adapter.InterfaceIndex;
            SelectedPlan.InterfaceAlias = adapter.Name;
            SelectedPlan.ApplyIpSettings = applyIpCheckBox.Checked;
            SelectedPlan.UseDhcp = dhcpRadio.Checked;
            SelectedPlan.IpAddress = ipAddressTextBox.Text.Trim();
            SelectedPlan.PrefixLength = prefix;
            SelectedPlan.Gateway = gatewayTextBox.Text.Trim();
            SelectedPlan.ApplyDnsSettings = applyDnsCheckBox.Checked;
            SelectedPlan.UseAutomaticDns = automaticDnsRadio.Checked;
            SelectedPlan.PrimaryDns = primaryDnsTextBox.Text.Trim();
            SelectedPlan.SecondaryDns = secondaryDnsTextBox.Text.Trim();
            SelectedPlan.FlushAndRenewDns = flushDnsCheckBox.Checked;
            SelectedPlan.ResetWinHttpProxy = resetProxyCheckBox.Checked;
            SelectedPlan.EnableTls12 = enableTlsCheckBox.Checked;
            SelectedPlan.ResetWinsockAndTcpIp = resetStackCheckBox.Checked;
            SelectedPlan.OpenInternetAdvancedOptions = openInternetOptionsCheckBox.Checked;
            SelectedPlan.TestConnectivity = testConnectivityCheckBox.Checked;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void TestIpFromDialog()
        {
            NetworkAdapterInfo adapter = adapterComboBox.SelectedItem as NetworkAdapterInfo;
            if (adapter == null)
            {
                MessageBox.Show(this, "Selecione um adaptador de rede.", "Adaptador obrigatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetIp = ipAddressTextBox.Text.Trim();
            if (!IsValidIpv4(targetIp))
            {
                MessageBox.Show(this, "Informe um endereco IPv4 valido antes de testar.", "IPv4 invalido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            IpAvailabilityState state = CheckIpAvailability(adapter, targetIp);
            if (state == IpAvailabilityState.CurrentAddress)
            {
                MessageBox.Show(this, "Este IPv4 ja pertence ao adaptador selecionado.", "IPv4 atual", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (state == IpAvailabilityState.InUse)
            {
                MessageBox.Show(this, "O IPv4 respondeu ao Ping ou apareceu na tabela ARP. Ele provavelmente ja esta em uso; escolha outro endereco.", "IPv4 ja esta em uso", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (state == IpAvailabilityState.Available)
            {
                MessageBox.Show(this, "Nenhuma resposta foi encontrada por Ping ou ARP. O IPv4 parece livre.\r\n\r\nObservacao: dispositivos podem bloquear Ping, portanto a verificacao sera repetida imediatamente antes da aplicacao.", "IPv4 aparentemente livre", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this, "Nao foi possivel concluir o teste. Verifique se os componentes de rede do Windows estao disponiveis.", "Verificacao inconclusiva", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private IpAvailabilityState CheckIpAvailability(NetworkAdapterInfo adapter, string targetIp)
        {
            bool previousWaitCursor = UseWaitCursor;
            bool previousButtonEnabled = testIpButton.Enabled;
            try
            {
                UseWaitCursor = true;
                testIpButton.Enabled = false;
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();

                string literalIp = "'" + targetIp.Replace("'", "''") + "'";
                string command =
                    "$target=" + literalIp + "; $idx=" + adapter.InterfaceIndex + "; " +
                    "$current=Get-NetIPAddress -InterfaceIndex $idx -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {$_.IPAddress -eq $target} | Select-Object -First 1; " +
                    "if($current){Write-Output 'CURRENT'; exit 0}; " +
                    "$localOther=Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {$_.IPAddress -eq $target -and $_.InterfaceIndex -ne $idx} | Select-Object -First 1; " +
                    "$ping=Test-Connection -ComputerName $target -Count 1 -Quiet -ErrorAction SilentlyContinue; " +
                    "Start-Sleep -Milliseconds 200; " +
                    "$neighbor=Get-NetNeighbor -InterfaceIndex $idx -IPAddress $target -ErrorAction SilentlyContinue | Where-Object {$_.State -notin @('Unreachable','Incomplete') -and ![string]::IsNullOrWhiteSpace([string]$_.LinkLayerAddress) -and $_.LinkLayerAddress -ne '00-00-00-00-00-00'} | Select-Object -First 1; " +
                    "if($localOther -or $ping -or $neighbor){Write-Output 'IN_USE'}else{Write-Output 'NO_RESPONSE'}";
                string[] lines = RunPowerShellLines(command);
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    string result = lines[i].Trim();
                    if (String.Equals(result, "CURRENT", StringComparison.OrdinalIgnoreCase)) return IpAvailabilityState.CurrentAddress;
                    if (String.Equals(result, "IN_USE", StringComparison.OrdinalIgnoreCase)) return IpAvailabilityState.InUse;
                    if (String.Equals(result, "NO_RESPONSE", StringComparison.OrdinalIgnoreCase)) return IpAvailabilityState.Available;
                }
                return IpAvailabilityState.Inconclusive;
            }
            catch
            {
                return IpAvailabilityState.Inconclusive;
            }
            finally
            {
                UseWaitCursor = previousWaitCursor;
                testIpButton.Enabled = previousButtonEnabled;
                Cursor = Cursors.Default;
            }
        }

        private bool IsValidIpv4(string value)
        {
            IPAddress address;
            return IPAddress.TryParse((value ?? "").Trim(), out address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }

        private void OpenInternetAdvancedOptions()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "control.exe";
                psi.Arguments = "inetcpl.cpl,,6";
                psi.UseShellExecute = true;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Nao foi possivel abrir as Opcoes da Internet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    internal sealed class SefazTimeZoneDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly ListBox timeZoneList = new ListBox();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();

        public SefazTimeZoneOption SelectedOption { get; private set; }

        public SefazTimeZoneDialog(SefazTimeZoneOption currentOption)
        {
            Text = "Selecionar UTC";
            Width = 600;
            Height = 360;
            MinimumSize = new Size(600, 360);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            LoadOptions(currentOption);
        }

        private void BuildLayout()
        {
            Label title = new Label();
            title.Text = "SSL/TLS 1.2 SEFAZ";
            title.Left = 24;
            title.Top = 18;
            title.Width = 420;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Selecione o UTC correto do cliente para sincronizar a hora do Windows.";
            subtitle.Left = 26;
            subtitle.Top = 58;
            subtitle.Width = 520;
            subtitle.Height = 26;
            subtitle.ForeColor = Color.FromArgb(82, 92, 110);
            Controls.Add(subtitle);

            timeZoneList.Left = 28;
            timeZoneList.Top = 98;
            timeZoneList.Width = 528;
            timeZoneList.Height = 150;
            timeZoneList.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
            timeZoneList.BorderStyle = BorderStyle.FixedSingle;
            timeZoneList.DoubleClick += delegate { ConfirmSelection(); };
            Controls.Add(timeZoneList);

            Label note = new Label();
            note.Text = "Geralmente: GO/SC/SP = UTC-3, AM/MT/MS = UTC-4, AC = UTC-5.";
            note.Left = 28;
            note.Top = 258;
            note.Width = 528;
            note.Height = 24;
            note.ForeColor = blue;
            Controls.Add(note);

            okButton.Text = "Salvar";
            okButton.Left = 354;
            okButton.Top = 292;
            okButton.Width = 96;
            okButton.Height = 36;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = border;
            okButton.BackColor = Color.White;
            okButton.ForeColor = Color.FromArgb(18, 24, 32);
            okButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            okButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(okButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 460;
            cancelButton.Top = 292;
            cancelButton.Width = 96;
            cancelButton.Height = 36;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadOptions(SefazTimeZoneOption currentOption)
        {
            SefazTimeZoneOption[] options = new SefazTimeZoneOption[]
            {
                new SefazTimeZoneOption("UTC-2 - Fernando de Noronha / ilhas oceanicas", "UTC-02"),
                new SefazTimeZoneOption("UTC-3 - Brasilia / Goiania / Balneario / maior parte do Brasil", "E. South America Standard Time"),
                new SefazTimeZoneOption("UTC-4 - Amazonas / Manaus / Rondonia / Roraima / MT / MS", "SA Western Standard Time"),
                new SefazTimeZoneOption("UTC-5 - Acre / sudoeste do Amazonas", "SA Pacific Standard Time")
            };

            timeZoneList.Items.AddRange(options);

            int selectedIndex = 1;

            if (currentOption != null)
            {
                for (int i = 0; i < timeZoneList.Items.Count; i++)
                {
                    SefazTimeZoneOption option = timeZoneList.Items[i] as SefazTimeZoneOption;

                    if (option != null && String.Equals(option.TimeZoneId, currentOption.TimeZoneId, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }

            timeZoneList.SelectedIndex = selectedIndex;
        }

        private void ConfirmSelection()
        {
            SefazTimeZoneOption option = timeZoneList.SelectedItem as SefazTimeZoneOption;

            if (option == null)
            {
                MessageBox.Show(this, "Selecione um UTC.", "UTC obrigatorio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedOption = option;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class ServerMigrationPlan
    {
        public const string DefaultFinalFolders = "ArqPrn;Atualizacao;CFe;Documentos;DocumentosFiscais;NFCe;NFe;Sngpc;versao;XML;Xml;xml;SAT;CTe;MDFe";

        public bool IsNovoServidor;
        public string HostAntigo = "SERVIDOR";
        public string TipoVersao = "normal";
        public bool CopiarPrincipal = true;
        public bool CopiarFinal;
        public bool InstalarFull = true;
        public bool InstalarFirebird = true;
        public bool ConfigurarRede = true;
        public bool RenomearReiniciar;
        public string ExcluirPastas = DefaultFinalFolders;

        public string GetSummary()
        {
            if (IsNovoServidor)
            {
                return "Novo servidor | origem " + HostAntigo + " | versao " + TipoVersao;
            }

            return "Servidor antigo | remover Firebird" + (RenomearReiniciar ? " | renomear para ANTIGO" : "");
        }
    }

    internal sealed class ServerMigrationDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly RadioButton novoRadio = new RadioButton();
        private readonly RadioButton antigoRadio = new RadioButton();
        private readonly TextBox hostAntigoTextBox = new TextBox();
        private readonly RadioButton versaoNormalRadio = new RadioButton();
        private readonly RadioButton versaoIRadio = new RadioButton();
        private readonly CheckBox copiarPrincipalCheckBox = new CheckBox();
        private readonly CheckBox copiarFinalCheckBox = new CheckBox();
        private readonly CheckBox instalarFullCheckBox = new CheckBox();
        private readonly CheckBox instalarFirebirdCheckBox = new CheckBox();
        private readonly CheckBox configurarRedeCheckBox = new CheckBox();
        private readonly CheckBox renomearReiniciarCheckBox = new CheckBox();
        private readonly TextBox excluirPastasTextBox = new TextBox();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();

        public ServerMigrationPlan SelectedPlan { get; private set; }

        public ServerMigrationDialog(ServerMigrationPlan currentPlan)
        {
            Text = "Troca de servidor";
            Width = 720;
            Height = 570;
            MinimumSize = new Size(720, 570);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            LoadPlan(currentPlan);
            UpdateModeState();
        }

        private void BuildLayout()
        {
            Label title = new Label();
            title.Text = "Troca de servidor";
            title.Left = 24;
            title.Top = 18;
            title.Width = 420;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            novoRadio.Text = "Configurar novo servidor";
            novoRadio.Left = 28;
            novoRadio.Top = 70;
            novoRadio.Width = 230;
            novoRadio.CheckedChanged += delegate { UpdateModeState(); };
            Controls.Add(novoRadio);

            antigoRadio.Text = "Configurar servidor antigo";
            antigoRadio.Left = 280;
            antigoRadio.Top = 70;
            antigoRadio.Width = 230;
            antigoRadio.CheckedChanged += delegate { UpdateModeState(); };
            Controls.Add(antigoRadio);

            GroupBox novoBox = new GroupBox();
            novoBox.Text = "Novo servidor";
            novoBox.Left = 24;
            novoBox.Top = 108;
            novoBox.Width = 660;
            novoBox.Height = 306;
            novoBox.ForeColor = blue;
            Controls.Add(novoBox);

            Label hostLabel = new Label();
            hostLabel.Text = "Servidor atual antes da renomeacao";
            hostLabel.Left = 16;
            hostLabel.Top = 30;
            hostLabel.Width = 260;
            hostLabel.Height = 22;
            hostLabel.ForeColor = Color.FromArgb(38, 48, 64);
            novoBox.Controls.Add(hostLabel);

            hostAntigoTextBox.Left = 16;
            hostAntigoTextBox.Top = 54;
            hostAntigoTextBox.Width = 300;
            hostAntigoTextBox.Height = 26;
            hostAntigoTextBox.Text = "SERVIDOR";
            novoBox.Controls.Add(hostAntigoTextBox);

            Label versionLabel = new Label();
            versionLabel.Text = "Versao do sistema";
            versionLabel.Left = 340;
            versionLabel.Top = 30;
            versionLabel.Width = 180;
            versionLabel.Height = 22;
            versionLabel.ForeColor = Color.FromArgb(38, 48, 64);
            novoBox.Controls.Add(versionLabel);

            versaoNormalRadio.Text = "Normal";
            versaoNormalRadio.Left = 340;
            versaoNormalRadio.Top = 56;
            versaoNormalRadio.Width = 90;
            novoBox.Controls.Add(versaoNormalRadio);

            versaoIRadio.Text = "Versao i";
            versaoIRadio.Left = 440;
            versaoIRadio.Top = 56;
            versaoIRadio.Width = 100;
            novoBox.Controls.Add(versaoIRadio);

            copiarPrincipalCheckBox.Text = "Pre-copiar arquivos principais sem pastas finais/pesadas";
            copiarPrincipalCheckBox.Left = 16;
            copiarPrincipalCheckBox.Top = 104;
            copiarPrincipalCheckBox.Width = 390;
            novoBox.Controls.Add(copiarPrincipalCheckBox);

            copiarFinalCheckBox.Text = "Copiar pastas finais por ultimo";
            copiarFinalCheckBox.Left = 16;
            copiarFinalCheckBox.Top = 134;
            copiarFinalCheckBox.Width = 360;
            novoBox.Controls.Add(copiarFinalCheckBox);

            instalarFullCheckBox.Text = "Executar FULL: versao + Crystal + .NET + VS";
            instalarFullCheckBox.Left = 16;
            instalarFullCheckBox.Top = 164;
            instalarFullCheckBox.Width = 360;
            novoBox.Controls.Add(instalarFullCheckBox);

            instalarFirebirdCheckBox.Text = "Instalar/reinstalar Firebird no novo servidor";
            instalarFirebirdCheckBox.Left = 16;
            instalarFirebirdCheckBox.Top = 194;
            instalarFirebirdCheckBox.Width = 360;
            novoBox.Controls.Add(instalarFirebirdCheckBox);

            configurarRedeCheckBox.Text = "Configurar rede e compartilhamento do sistema";
            configurarRedeCheckBox.Left = 16;
            configurarRedeCheckBox.Top = 224;
            configurarRedeCheckBox.Width = 360;
            novoBox.Controls.Add(configurarRedeCheckBox);

            Label excluirLabel = new Label();
            excluirLabel.Text = "Pastas para deixar por ultimo";
            excluirLabel.Left = 16;
            excluirLabel.Top = 254;
            excluirLabel.Width = 220;
            excluirLabel.Height = 22;
            excluirLabel.ForeColor = Color.FromArgb(38, 48, 64);
            novoBox.Controls.Add(excluirLabel);

            excluirPastasTextBox.Left = 234;
            excluirPastasTextBox.Top = 252;
            excluirPastasTextBox.Width = 398;
            excluirPastasTextBox.Height = 26;
            novoBox.Controls.Add(excluirPastasTextBox);

            GroupBox antigoBox = new GroupBox();
            antigoBox.Text = "Servidor antigo";
            antigoBox.Left = 24;
            antigoBox.Top = 424;
            antigoBox.Width = 660;
            antigoBox.Height = 72;
            antigoBox.ForeColor = blue;
            Controls.Add(antigoBox);

            renomearReiniciarCheckBox.Text = "Renomear este computador para ANTIGO/SERVIDOR e reiniciar";
            renomearReiniciarCheckBox.Left = 16;
            renomearReiniciarCheckBox.Top = 30;
            renomearReiniciarCheckBox.Width = 520;
            antigoBox.Controls.Add(renomearReiniciarCheckBox);

            okButton.Text = "Salvar";
            okButton.Left = 482;
            okButton.Top = 510;
            okButton.Width = 96;
            okButton.Height = 36;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = border;
            okButton.BackColor = Color.White;
            okButton.ForeColor = Color.FromArgb(18, 24, 32);
            okButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            okButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(okButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 588;
            cancelButton.Top = 510;
            cancelButton.Width = 96;
            cancelButton.Height = 36;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadPlan(ServerMigrationPlan plan)
        {
            if (plan == null)
            {
                novoRadio.Checked = true;
                versaoNormalRadio.Checked = true;
                copiarPrincipalCheckBox.Checked = true;
                copiarFinalCheckBox.Checked = false;
                instalarFullCheckBox.Checked = true;
                instalarFirebirdCheckBox.Checked = true;
                configurarRedeCheckBox.Checked = true;
                renomearReiniciarCheckBox.Checked = false;
                excluirPastasTextBox.Text = ServerMigrationPlan.DefaultFinalFolders;
                return;
            }

            novoRadio.Checked = plan.IsNovoServidor;
            antigoRadio.Checked = !plan.IsNovoServidor;
            hostAntigoTextBox.Text = plan.HostAntigo;
            versaoNormalRadio.Checked = !String.Equals(plan.TipoVersao, "i", StringComparison.OrdinalIgnoreCase);
            versaoIRadio.Checked = String.Equals(plan.TipoVersao, "i", StringComparison.OrdinalIgnoreCase);
            copiarPrincipalCheckBox.Checked = plan.CopiarPrincipal;
            copiarFinalCheckBox.Checked = plan.CopiarFinal;
            instalarFullCheckBox.Checked = plan.InstalarFull;
            instalarFirebirdCheckBox.Checked = plan.InstalarFirebird;
            configurarRedeCheckBox.Checked = plan.ConfigurarRede;
            renomearReiniciarCheckBox.Checked = plan.RenomearReiniciar;
            excluirPastasTextBox.Text = plan.ExcluirPastas;
        }

        private void UpdateModeState()
        {
            bool novo = novoRadio.Checked;
            hostAntigoTextBox.Enabled = novo;
            versaoNormalRadio.Enabled = novo;
            versaoIRadio.Enabled = novo;
            copiarPrincipalCheckBox.Enabled = novo;
            copiarFinalCheckBox.Enabled = novo;
            instalarFullCheckBox.Enabled = novo;
            instalarFirebirdCheckBox.Enabled = novo;
            configurarRedeCheckBox.Enabled = novo;
            excluirPastasTextBox.Enabled = novo;
        }

        private void ConfirmSelection()
        {
            ServerMigrationPlan plan = new ServerMigrationPlan();
            plan.IsNovoServidor = novoRadio.Checked;
            plan.HostAntigo = String.IsNullOrWhiteSpace(hostAntigoTextBox.Text) ? "SERVIDOR" : hostAntigoTextBox.Text.Trim();
            plan.TipoVersao = versaoIRadio.Checked ? "i" : "normal";
            plan.CopiarPrincipal = copiarPrincipalCheckBox.Checked;
            plan.CopiarFinal = copiarFinalCheckBox.Checked;
            plan.InstalarFull = instalarFullCheckBox.Checked;
            plan.InstalarFirebird = instalarFirebirdCheckBox.Checked;
            plan.ConfigurarRede = configurarRedeCheckBox.Checked;
            plan.RenomearReiniciar = renomearReiniciarCheckBox.Checked;
            plan.ExcluirPastas = excluirPastasTextBox.Text.Trim();

            SelectedPlan = plan;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal sealed class PrinterDriver
    {
        public string marca { get; set; }
        public string modelo { get; set; }
        public string arquivo { get; set; }
        public string instalador { get; set; }
        public string[] instaladores { get; set; }
        public string origem { get; set; }
        public long tamanhoBytes { get; set; }

        public string BrandName
        {
            get { return marca ?? ""; }
        }

        public string ModelName
        {
            get { return modelo ?? ""; }
        }

        public string AssetFile
        {
            get { return arquivo ?? ""; }
        }

        public string InstallerPath
        {
            get { return instalador ?? ""; }
        }

        public string DisplayText
        {
            get
            {
                string text = ModelName;

                if (tamanhoBytes > 0)
                {
                    text += "  (" + FormatBytes(tamanhoBytes) + ")";
                }

                return text;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return Math.Round(bytes / 1024.0 / 1024.0, 1) + " MB";
            }

            if (bytes >= 1024)
            {
                return Math.Round(bytes / 1024.0, 1) + " KB";
            }

            return bytes + " bytes";
        }
    }

    internal sealed class PrinterDriverDialog : Form
    {
        private readonly string indexUrl;
        private readonly PrinterDriver currentDriver;
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly ListBox brandList = new ListBox();
        private readonly ListBox modelList = new ListBox();
        private readonly Label statusLabel = new Label();
        private readonly Label detailLabel = new Label();
        private readonly Button refreshButton = new Button();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly List<PrinterDriver> drivers = new List<PrinterDriver>();

        public PrinterDriver SelectedDriver { get; private set; }

        public PrinterDriverDialog(string indexUrl, PrinterDriver currentDriver)
        {
            this.indexUrl = indexUrl;
            this.currentDriver = currentDriver;

            Text = "Selecionar impressora";
            Width = 760;
            Height = 500;
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            Load += delegate { LoadIndex(); };
        }

        private void BuildLayout()
        {
            Label title = new Label();
            title.Text = "Instalar impressora";
            title.Left = 24;
            title.Top = 18;
            title.Width = 420;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            statusLabel.Text = "Carregando indice de drivers...";
            statusLabel.Left = 26;
            statusLabel.Top = 58;
            statusLabel.Width = 560;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.FromArgb(90, 98, 112);
            Controls.Add(statusLabel);

            refreshButton.Text = "Atualizar";
            refreshButton.Left = 628;
            refreshButton.Top = 24;
            refreshButton.Width = 96;
            refreshButton.Height = 32;
            refreshButton.FlatStyle = FlatStyle.Flat;
            refreshButton.FlatAppearance.BorderColor = border;
            refreshButton.BackColor = Color.White;
            refreshButton.Click += delegate { LoadIndex(); };
            Controls.Add(refreshButton);

            Label brandTitle = new Label();
            brandTitle.Text = "Marca";
            brandTitle.Left = 24;
            brandTitle.Top = 96;
            brandTitle.Width = 250;
            brandTitle.Height = 24;
            brandTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            brandTitle.ForeColor = blue;
            Controls.Add(brandTitle);

            brandList.Left = 24;
            brandList.Top = 124;
            brandList.Width = 300;
            brandList.Height = 244;
            brandList.BorderStyle = BorderStyle.FixedSingle;
            brandList.SelectedIndexChanged += delegate { PopulateModels(); };
            Controls.Add(brandList);

            Label modelTitle = new Label();
            modelTitle.Text = "Modelo";
            modelTitle.Left = 348;
            modelTitle.Top = 96;
            modelTitle.Width = 250;
            modelTitle.Height = 24;
            modelTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            modelTitle.ForeColor = blue;
            Controls.Add(modelTitle);

            modelList.Left = 348;
            modelList.Top = 124;
            modelList.Width = 376;
            modelList.Height = 244;
            modelList.BorderStyle = BorderStyle.FixedSingle;
            modelList.DisplayMember = "DisplayText";
            modelList.SelectedIndexChanged += delegate { UpdateSelectionDetails(); };
            modelList.DoubleClick += delegate { ConfirmSelection(); };
            Controls.Add(modelList);

            detailLabel.Text = "Selecione uma marca e um modelo.";
            detailLabel.Left = 24;
            detailLabel.Top = 382;
            detailLabel.Width = 700;
            detailLabel.Height = 36;
            detailLabel.AutoEllipsis = true;
            detailLabel.ForeColor = Color.FromArgb(38, 48, 64);
            Controls.Add(detailLabel);

            okButton.Text = "Instalar selecionada";
            okButton.Left = 462;
            okButton.Top = 424;
            okButton.Width = 150;
            okButton.Height = 36;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = border;
            okButton.BackColor = Color.White;
            okButton.ForeColor = Color.FromArgb(18, 24, 32);
            okButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            okButton.Enabled = false;
            okButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(okButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 628;
            cancelButton.Top = 424;
            cancelButton.Width = 96;
            cancelButton.Height = 36;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadIndex()
        {
            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            refreshButton.Enabled = false;
            okButton.Enabled = false;
            statusLabel.Text = "Baixando indice de drivers...";
            brandList.Items.Clear();
            modelList.Items.Clear();
            detailLabel.Text = "Aguarde o carregamento do indice.";

            try
            {
                using (WebClient client = new WebClient())
                {
                    string json = client.DownloadString(indexUrl);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    PrinterDriver[] loaded = serializer.Deserialize<PrinterDriver[]>(json);

                    drivers.Clear();

                    if (loaded != null)
                    {
                        for (int i = 0; i < loaded.Length; i++)
                        {
                            if (loaded[i] != null && !String.IsNullOrWhiteSpace(loaded[i].BrandName) && !String.IsNullOrWhiteSpace(loaded[i].ModelName) && !String.IsNullOrWhiteSpace(loaded[i].AssetFile))
                            {
                                drivers.Add(loaded[i]);
                            }
                        }
                    }
                }

                drivers.Sort(delegate(PrinterDriver a, PrinterDriver b)
                {
                    int brand = String.Compare(a.BrandName, b.BrandName, StringComparison.OrdinalIgnoreCase);
                    if (brand != 0) return brand;
                    return String.Compare(a.ModelName, b.ModelName, StringComparison.OrdinalIgnoreCase);
                });

                PopulateBrands();
                statusLabel.Text = drivers.Count + " drivers disponiveis.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erro ao carregar indice de drivers.";
                detailLabel.Text = ex.Message;
            }
            finally
            {
                refreshButton.Enabled = true;
                Cursor.Current = previousCursor;
            }
        }

        private void PopulateBrands()
        {
            brandList.Items.Clear();
            modelList.Items.Clear();

            List<string> brands = new List<string>();

            for (int i = 0; i < drivers.Count; i++)
            {
                if (!ContainsText(brands, drivers[i].BrandName))
                {
                    brands.Add(drivers[i].BrandName);
                }
            }

            brands.Sort(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < brands.Count; i++)
            {
                brandList.Items.Add(brands[i]);
            }

            if (currentDriver != null)
            {
                for (int i = 0; i < brandList.Items.Count; i++)
                {
                    if (String.Equals(Convert.ToString(brandList.Items[i]), currentDriver.BrandName, StringComparison.OrdinalIgnoreCase))
                    {
                        brandList.SelectedIndex = i;
                        SelectCurrentModel();
                        return;
                    }
                }
            }

            if (brandList.Items.Count > 0)
            {
                brandList.SelectedIndex = 0;
            }
        }

        private void PopulateModels()
        {
            modelList.Items.Clear();
            string brand = Convert.ToString(brandList.SelectedItem);

            for (int i = 0; i < drivers.Count; i++)
            {
                if (String.Equals(drivers[i].BrandName, brand, StringComparison.OrdinalIgnoreCase))
                {
                    modelList.Items.Add(drivers[i]);
                }
            }

            if (modelList.Items.Count > 0)
            {
                modelList.SelectedIndex = 0;
            }

            SelectCurrentModel();
            UpdateSelectionDetails();
        }

        private void SelectCurrentModel()
        {
            if (currentDriver == null)
            {
                return;
            }

            for (int i = 0; i < modelList.Items.Count; i++)
            {
                PrinterDriver driver = modelList.Items[i] as PrinterDriver;
                if (driver != null &&
                    String.Equals(driver.BrandName, currentDriver.BrandName, StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(driver.ModelName, currentDriver.ModelName, StringComparison.OrdinalIgnoreCase))
                {
                    modelList.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UpdateSelectionDetails()
        {
            PrinterDriver driver = modelList.SelectedItem as PrinterDriver;
            okButton.Enabled = driver != null;

            if (driver == null)
            {
                detailLabel.Text = "Selecione uma marca e um modelo.";
                return;
            }

            string installer = String.IsNullOrWhiteSpace(driver.InstallerPath) ? "instalador automatico" : driver.InstallerPath;
            detailLabel.Text = "Arquivo: " + driver.AssetFile + " | Instalador: " + installer;
        }

        private void ConfirmSelection()
        {
            PrinterDriver driver = modelList.SelectedItem as PrinterDriver;

            if (driver == null)
            {
                return;
            }

            SelectedDriver = driver;
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ContainsText(List<string> values, string text)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (String.Equals(values[i], text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class PrinterRemovalDialog : Form
    {
        private readonly Color blue = Color.FromArgb(11, 67, 112);
        private readonly Color darkBlue = Color.FromArgb(7, 45, 75);
        private readonly Color border = Color.FromArgb(211, 225, 232);
        private readonly CheckedListBox printerList = new CheckedListBox();
        private readonly CheckedListBox driverList = new CheckedListBox();
        private readonly Label statusLabel = new Label();
        private readonly Button refreshButton = new Button();
        private readonly Button okButton = new Button();
        private readonly Button cancelButton = new Button();

        public List<string> SelectedPrinters { get; private set; }
        public List<string> SelectedDrivers { get; private set; }

        public PrinterRemovalDialog()
        {
            SelectedPrinters = new List<string>();
            SelectedDrivers = new List<string>();

            Text = "Remover impressoras e drivers";
            Width = 820;
            Height = 540;
            MinimumSize = new Size(820, 540);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            Load += delegate { LoadInstalledItems(); };
        }

        private void BuildLayout()
        {
            Label title = new Label();
            title.Text = "Remover impressoras e drivers";
            title.Left = 24;
            title.Top = 18;
            title.Width = 460;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            statusLabel.Text = "Selecione somente os itens que deseja remover.";
            statusLabel.Left = 26;
            statusLabel.Top = 58;
            statusLabel.Width = 650;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.FromArgb(90, 98, 112);
            Controls.Add(statusLabel);

            refreshButton.Text = "Atualizar";
            refreshButton.Left = 686;
            refreshButton.Top = 24;
            refreshButton.Width = 96;
            refreshButton.Height = 32;
            refreshButton.FlatStyle = FlatStyle.Flat;
            refreshButton.FlatAppearance.BorderColor = border;
            refreshButton.BackColor = Color.White;
            refreshButton.Click += delegate { LoadInstalledItems(); };
            Controls.Add(refreshButton);

            Label printersTitle = new Label();
            printersTitle.Text = "Impressoras instaladas";
            printersTitle.Left = 24;
            printersTitle.Top = 96;
            printersTitle.Width = 340;
            printersTitle.Height = 24;
            printersTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            printersTitle.ForeColor = blue;
            Controls.Add(printersTitle);

            printerList.Left = 24;
            printerList.Top = 124;
            printerList.Width = 370;
            printerList.Height = 300;
            printerList.CheckOnClick = true;
            printerList.BorderStyle = BorderStyle.FixedSingle;
            printerList.DisplayMember = "DisplayText";
            Controls.Add(printerList);

            Label driversTitle = new Label();
            driversTitle.Text = "Drivers instalados";
            driversTitle.Left = 416;
            driversTitle.Top = 96;
            driversTitle.Width = 340;
            driversTitle.Height = 24;
            driversTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            driversTitle.ForeColor = blue;
            Controls.Add(driversTitle);

            driverList.Left = 416;
            driverList.Top = 124;
            driverList.Width = 366;
            driverList.Height = 300;
            driverList.CheckOnClick = true;
            driverList.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(driverList);

            Label warning = new Label();
            warning.Text = "Dica: para liberar um driver em uso, marque tambem as impressoras associadas a ele.";
            warning.Left = 24;
            warning.Top = 436;
            warning.Width = 758;
            warning.Height = 24;
            warning.ForeColor = Color.FromArgb(110, 80, 32);
            Controls.Add(warning);

            okButton.Text = "Confirmar";
            okButton.Left = 574;
            okButton.Top = 466;
            okButton.Width = 100;
            okButton.Height = 36;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = border;
            okButton.BackColor = Color.White;
            okButton.ForeColor = Color.FromArgb(18, 24, 32);
            okButton.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            okButton.Click += delegate { ConfirmSelection(); };
            Controls.Add(okButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 686;
            cancelButton.Top = 466;
            cancelButton.Width = 96;
            cancelButton.Height = 36;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void LoadInstalledItems()
        {
            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            refreshButton.Enabled = false;
            statusLabel.Text = "Carregando impressoras e drivers instalados...";
            printerList.Items.Clear();
            driverList.Items.Clear();

            try
            {
                List<InstalledPrinterInfo> printers = QueryInstalledPrinters();
                for (int i = 0; i < printers.Count; i++)
                {
                    printerList.Items.Add(printers[i], false);
                }

                List<string> drivers = QueryInstalledDrivers();
                for (int i = 0; i < drivers.Count; i++)
                {
                    driverList.Items.Add(drivers[i], false);
                }

                statusLabel.Text = printers.Count + " impressoras e " + drivers.Count + " drivers encontrados.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erro ao carregar impressoras/drivers.";
                MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                refreshButton.Enabled = true;
                Cursor.Current = previousCursor;
            }
        }

        private List<InstalledPrinterInfo> QueryInstalledPrinters()
        {
            List<InstalledPrinterInfo> items = new List<InstalledPrinterInfo>();
            string[] lines = RunPowerShellLines("Get-Printer | Sort-Object Name | ForEach-Object { $_.Name + [char]9 + $_.DriverName }");

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (String.IsNullOrWhiteSpace(line)) continue;

                string[] parts = line.Split(new char[] { '\t' }, 2);
                string name = parts.Length > 0 ? parts[0].Trim() : "";
                string driver = parts.Length > 1 ? parts[1].Trim() : "";

                if (!String.IsNullOrWhiteSpace(name))
                {
                    items.Add(new InstalledPrinterInfo(name, driver));
                }
            }

            return items;
        }

        private List<string> QueryInstalledDrivers()
        {
            List<string> items = new List<string>();
            string[] lines = RunPowerShellLines("Get-PrinterDriver | Sort-Object Name | ForEach-Object { $_.Name }");

            for (int i = 0; i < lines.Length; i++)
            {
                string name = lines[i].Trim();

                if (!String.IsNullOrWhiteSpace(name) && !ContainsText(items, name))
                {
                    items.Add(name);
                }
            }

            return items;
        }

        private string[] RunPowerShellLines(string command)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + QuoteForPowerShell(command);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);

                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { }
                    throw new InvalidOperationException("Tempo limite ao consultar impressoras/drivers.");
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(String.IsNullOrWhiteSpace(stderr) ? "Falha ao consultar impressoras/drivers." : stderr.Trim());
                }

                return stdout.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        private string QuoteForPowerShell(string value)
        {
            if (value == null) value = "";
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private void ConfirmSelection()
        {
            SelectedPrinters.Clear();
            SelectedDrivers.Clear();

            for (int i = 0; i < printerList.CheckedItems.Count; i++)
            {
                InstalledPrinterInfo printer = printerList.CheckedItems[i] as InstalledPrinterInfo;
                if (printer != null && !String.IsNullOrWhiteSpace(printer.Name))
                {
                    SelectedPrinters.Add(printer.Name);
                }
            }

            for (int i = 0; i < driverList.CheckedItems.Count; i++)
            {
                string driver = Convert.ToString(driverList.CheckedItems[i]);
                if (!String.IsNullOrWhiteSpace(driver))
                {
                    SelectedDrivers.Add(driver);
                }
            }

            if (SelectedPrinters.Count == 0 && SelectedDrivers.Count == 0)
            {
                MessageBox.Show(this, "Selecione ao menos uma impressora ou driver.", "Selecao obrigatoria", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ContainsText(List<string> values, string text)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (String.Equals(values[i], text, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class InstalledPrinterInfo
    {
        public readonly string Name;
        public readonly string DriverName;

        public InstalledPrinterInfo(string name, string driverName)
        {
            Name = name;
            DriverName = driverName;
        }

        public string DisplayText
        {
            get
            {
                if (String.IsNullOrWhiteSpace(DriverName))
                {
                    return Name;
                }

                return Name + "  |  " + DriverName;
            }
        }
    }

    internal enum SectionIconKind
    {
        Command,
        Network,
        Certificate,
        Apps,
        Software,
        Server,
        Printer,
        Windows
    }

    internal sealed class SectionIcon : Panel
    {
        private readonly SectionIconKind kind;

        public SectionIcon(SectionIconKind kind)
        {
            this.kind = kind;
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (Pen p = new Pen(ForeColor, 1.8F))
            using (Brush b = new SolidBrush(ForeColor))
            {
                switch (kind)
                {
                    case SectionIconKind.Command:
                        DrawCommand(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Network:
                        DrawNetwork(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Certificate:
                        DrawCertificate(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Apps:
                        DrawApps(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Software:
                        DrawSoftware(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Server:
                        DrawServer(e.Graphics, p, b);
                        break;
                    case SectionIconKind.Printer:
                        DrawPrinter(e.Graphics, p, b);
                        break;
                    default:
                        DrawWindows(e.Graphics, p, b);
                        break;
                }
            }
        }

        private void DrawCommand(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 1, 2, 16, 14);
            g.DrawLine(p, 4, 6, 7, 9);
            g.DrawLine(p, 7, 9, 4, 12);
            g.DrawLine(p, 9, 12, 14, 12);
        }

        private void DrawNetwork(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 7, 1, 4, 4);
            g.DrawRectangle(p, 1, 12, 4, 4);
            g.DrawRectangle(p, 13, 12, 4, 4);
            g.DrawLine(p, 9, 5, 9, 9);
            g.DrawLine(p, 9, 9, 3, 12);
            g.DrawLine(p, 9, 9, 15, 12);
        }

        private void DrawCertificate(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 3, 1, 12, 14);
            g.DrawLine(p, 6, 5, 12, 5);
            g.DrawLine(p, 6, 8, 12, 8);
            g.FillEllipse(b, 6, 10, 6, 6);
            g.DrawLine(p, 7, 15, 6, 17);
            g.DrawLine(p, 11, 15, 12, 17);
        }

        private void DrawApps(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 2, 2, 5, 5);
            g.DrawRectangle(p, 11, 2, 5, 5);
            g.DrawRectangle(p, 2, 11, 5, 5);
            g.DrawRectangle(p, 11, 11, 5, 5);
        }

        private void DrawSoftware(Graphics g, Pen p, Brush b)
        {
            Point[] box = new Point[]
            {
                new Point(9, 1), new Point(16, 5), new Point(16, 13),
                new Point(9, 17), new Point(2, 13), new Point(2, 5)
            };
            g.DrawPolygon(p, box);
            g.DrawLine(p, 2, 5, 9, 9);
            g.DrawLine(p, 16, 5, 9, 9);
            g.DrawLine(p, 9, 9, 9, 17);
        }

        private void DrawServer(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 3, 2, 12, 5);
            g.DrawRectangle(p, 3, 10, 12, 5);
            g.FillEllipse(b, 5, 4, 1.8F, 1.8F);
            g.FillEllipse(b, 5, 12, 1.8F, 1.8F);
            g.DrawLine(p, 8, 4, 13, 4);
            g.DrawLine(p, 8, 12, 13, 12);
        }

        private void DrawPrinter(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 4, 1, 10, 5);
            g.DrawRectangle(p, 2, 7, 14, 7);
            g.DrawRectangle(p, 5, 12, 8, 5);
            g.FillEllipse(b, 13, 9, 1.8F, 1.8F);
        }

        private void DrawWindows(Graphics g, Pen p, Brush b)
        {
            g.DrawRectangle(p, 2, 2, 6, 6);
            g.DrawRectangle(p, 10, 2, 6, 6);
            g.DrawRectangle(p, 2, 10, 6, 6);
            g.DrawRectangle(p, 10, 10, 6, 6);
        }
    }

    internal sealed class GearPanel : Panel
    {
        public GearPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int cx = Width / 2;
            int cy = Height / 2;
            Color blue = Color.FromArgb(8, 154, 103);

            using (Brush b = new SolidBrush(blue))
            {
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    int x = cx + (int)(Math.Cos(angle) * 11) - 3;
                    int y = cy + (int)(Math.Sin(angle) * 11) - 3;
                    e.Graphics.FillRectangle(b, x, y, 6, 6);
                }

                e.Graphics.FillEllipse(b, 5, 5, Width - 10, Height - 10);
            }

            using (Brush white = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(white, cx - 5, cy - 5, 10, 10);
            }
        }
    }

    internal sealed class InfoCircle : Panel
    {
        public InfoCircle()
        {
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (Pen p = new Pen(ForeColor, 2F))
            {
                e.Graphics.DrawEllipse(p, 2, 2, Width - 5, Height - 5);
            }

            using (Brush b = new SolidBrush(ForeColor))
            using (Font f = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point))
            {
                StringFormat sf = new StringFormat();
                sf.Alignment = StringAlignment.Center;
                sf.LineAlignment = StringAlignment.Center;
                e.Graphics.DrawString("i", f, b, ClientRectangle, sf);
            }
        }
    }
}
