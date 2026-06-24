using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TekFarmaInstaller
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }

    internal sealed class InstallerForm : Form
    {
        private const string Version = "v1.0";
        private const string Repo = "Nata-Felix/Instalacao_crystal_adv";
        private const string BaseUrl = "https://github.com/" + Repo + "/releases/download/" + Version;
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repo + "/main";
        private const string UrlVersaoNormal = "https://files.tekfarma.com.br/versao/TekFarma50.exe";
        private const string UrlVersaoI = "https://files.tekfarma.com.br/versao/TekFarma50i.exe";
        private const string UrlBancoTekFarma = "https://files.tekfarma.com.br/util/TEKFARMA(NOV-2020).zip";

        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color lightBlue = Color.FromArgb(232, 244, 255);
        private readonly Color border = Color.FromArgb(205, 214, 224);
        private readonly Color textMuted = Color.FromArgb(110, 119, 135);

        private readonly List<OptionRow> optionRows = new List<OptionRow>();
        private readonly RadioButton normalRadio = new RadioButton();
        private readonly RadioButton iRadio = new RadioButton();
        private readonly RadioButton servidorRadio = new RadioButton();
        private readonly RadioButton terminalRadio = new RadioButton();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly Button installButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly CheckBox keepOpenWhenDoneCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly PictureBox logoBox = new PictureBox();

        private BackgroundWorker worker;
        private Process runningProcess;
        private volatile bool cancelRequested;
        private string tempDir;

        public InstallerForm()
        {
            Text = "Instalador TekFarma / Crystal";
            Width = 1024;
            Height = 660;
            MinimumSize = new Size(1024, 660);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            FormClosing += InstallerForm_FormClosing;

            Icon icon = LoadEmbeddedIcon("TekFarmaIcon");
            if (icon != null)
            {
                Icon = icon;
            }

            BuildLayout();
            SelectMode(InstallMode.Versao);
            UpdateProfileControls();
        }

        private void BuildLayout()
        {
            Panel root = new Panel();
            root.Dock = DockStyle.Fill;
            root.BackColor = Color.White;
            Controls.Add(root);

            BuildHeader(root);
            BuildContent(root);
            BuildFooter(root);
        }

        private void BuildHeader(Control root)
        {
            Panel header = new Panel();
            header.Left = 24;
            header.Top = 10;
            header.Width = 958;
            header.Height = 130;
            header.BackColor = Color.White;
            root.Controls.Add(header);

            Image logo = LoadEmbeddedImage("TekFarmaLogo");
            if (logo != null)
            {
                logoBox.Image = logo;
                logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            }

            logoBox.Left = 20;
            logoBox.Top = 10;
            logoBox.Width = 295;
            logoBox.Height = 100;
            header.Controls.Add(logoBox);

            Panel divider = new Panel();
            divider.Left = 345;
            divider.Top = 20;
            divider.Width = 1;
            divider.Height = 84;
            divider.BackColor = border;
            header.Controls.Add(divider);

            Label title = new Label();
            title.Text = "Instalador TekFarma / Crystal";
            title.Left = 380;
            title.Top = 26;
            title.Width = 520;
            title.Height = 42;
            title.Font = new Font("Segoe UI", 23F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Assistente de instalacao e configuracao";
            subtitle.Left = 384;
            subtitle.Top = 72;
            subtitle.Width = 520;
            subtitle.Height = 28;
            subtitle.Font = new Font("Segoe UI", 14F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = textMuted;
            header.Controls.Add(subtitle);

            Panel horizontal = new Panel();
            horizontal.Left = 0;
            horizontal.Top = 122;
            horizontal.Width = 958;
            horizontal.Height = 1;
            horizontal.BackColor = border;
            header.Controls.Add(horizontal);
        }

        private void BuildContent(Control root)
        {
            Label selectLabel = new Label();
            selectLabel.Text = "Selecione o modo de instalacao";
            selectLabel.Left = 44;
            selectLabel.Top = 158;
            selectLabel.Width = 360;
            selectLabel.Height = 24;
            selectLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            selectLabel.ForeColor = blue;
            root.Controls.Add(selectLabel);

            Panel optionsPanel = new Panel();
            optionsPanel.Left = 42;
            optionsPanel.Top = 186;
            optionsPanel.Width = 336;
            optionsPanel.Height = 318;
            optionsPanel.BackColor = Color.White;
            optionsPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(border))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, optionsPanel.Width - 1, optionsPanel.Height - 1);
                }
            };
            root.Controls.Add(optionsPanel);

            AddOption(optionsPanel, InstallMode.Versao, 8, "Instalacao somente versao", "", "box");
            AddOption(optionsPanel, InstallMode.Crystal, 70, "Instalacao somente Crystal", "", "diamond");
            AddOption(optionsPanel, InstallMode.Full, 132, "Instalacao FULL: versao + VS +", "DotNet + Crystal", "stack");
            AddOption(optionsPanel, InstallMode.SemiFull, 194, "Instalacao SEMI-FULL:", "versao + Crystal", "cube");
            AddOption(optionsPanel, InstallMode.TekFarma, 256, "Instalacao TekFarma", "servidor / terminal", "network");

            BuildChoicePanel(root);
            BuildProgressPanel(root);
        }

        private void AddOption(Panel parent, InstallMode mode, int top, string title, string subtitle, string icon)
        {
            OptionRow row = new OptionRow(mode, title, subtitle, icon);
            row.Left = 8;
            row.Top = top;
            row.Width = 320;
            row.Height = 54;
            row.SelectedChanged += delegate { SelectMode(row.Mode); };
            parent.Controls.Add(row);
            optionRows.Add(row);
        }

        private void BuildChoicePanel(Control root)
        {
            Panel choicePanel = new Panel();
            choicePanel.Left = 42;
            choicePanel.Top = 510;
            choicePanel.Width = 336;
            choicePanel.Height = 54;
            choicePanel.BackColor = Color.White;
            root.Controls.Add(choicePanel);

            Panel versionPanel = new Panel();
            versionPanel.Left = 0;
            versionPanel.Top = 0;
            versionPanel.Width = 150;
            versionPanel.Height = 54;
            versionPanel.BackColor = Color.White;
            choicePanel.Controls.Add(versionPanel);

            Panel profilePanel = new Panel();
            profilePanel.Left = 165;
            profilePanel.Top = 0;
            profilePanel.Width = 150;
            profilePanel.Height = 54;
            profilePanel.BackColor = Color.White;
            choicePanel.Controls.Add(profilePanel);

            normalRadio.Text = "Versao normal";
            normalRadio.Left = 4;
            normalRadio.Top = 4;
            normalRadio.Width = 128;
            normalRadio.Height = 22;
            normalRadio.Checked = true;
            normalRadio.CheckedChanged += delegate { UpdateProfileControls(); };
            versionPanel.Controls.Add(normalRadio);

            iRadio.Text = "Versao i";
            iRadio.Left = 4;
            iRadio.Top = 28;
            iRadio.Width = 128;
            iRadio.Height = 22;
            versionPanel.Controls.Add(iRadio);

            servidorRadio.Text = "Servidor";
            servidorRadio.Left = 0;
            servidorRadio.Top = 4;
            servidorRadio.Width = 118;
            servidorRadio.Height = 22;
            servidorRadio.Checked = true;
            servidorRadio.CheckedChanged += delegate { UpdateProfileControls(); };
            profilePanel.Controls.Add(servidorRadio);

            terminalRadio.Text = "Terminal";
            terminalRadio.Left = 0;
            terminalRadio.Top = 28;
            terminalRadio.Width = 118;
            terminalRadio.Height = 22;
            terminalRadio.CheckedChanged += delegate { UpdateProfileControls(); };
            profilePanel.Controls.Add(terminalRadio);
        }

        private void BuildProgressPanel(Control root)
        {
            Label progressTitle = new Label();
            progressTitle.Text = "Progresso da execucao";
            progressTitle.Left = 420;
            progressTitle.Top = 158;
            progressTitle.Width = 360;
            progressTitle.Height = 24;
            progressTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            progressTitle.ForeColor = blue;
            root.Controls.Add(progressTitle);

            progressBar.Left = 420;
            progressBar.Top = 196;
            progressBar.Width = 480;
            progressBar.Height = 28;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            root.Controls.Add(progressBar);

            progressLabel.Text = "0% concluido";
            progressLabel.Left = 920;
            progressLabel.Top = 199;
            progressLabel.Width = 90;
            progressLabel.Height = 24;
            progressLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            progressLabel.ForeColor = blue;
            root.Controls.Add(progressLabel);

            GearPanel gear = new GearPanel();
            gear.Left = 420;
            gear.Top = 240;
            gear.Width = 34;
            gear.Height = 34;
            gear.ForeColor = Color.FromArgb(24, 118, 224);
            root.Controls.Add(gear);

            currentStepLabel.Text = "Aguardando inicio da instalacao";
            currentStepLabel.Left = 462;
            currentStepLabel.Top = 244;
            currentStepLabel.Width = 510;
            currentStepLabel.Height = 28;
            currentStepLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            currentStepLabel.ForeColor = Color.FromArgb(35, 43, 55);
            root.Controls.Add(currentStepLabel);

            Label logTitle = new Label();
            logTitle.Text = "Log de execucao (PowerShell)";
            logTitle.Left = 420;
            logTitle.Top = 294;
            logTitle.Width = 360;
            logTitle.Height = 24;
            logTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            logTitle.ForeColor = blue;
            root.Controls.Add(logTitle);

            logBox.Left = 420;
            logBox.Top = 322;
            logBox.Width = 560;
            logBox.Height = 182;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.BackColor = Color.White;
            logBox.ForeColor = Color.FromArgb(24, 30, 38);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(logBox);
        }

        private void BuildFooter(Control root)
        {
            Panel line = new Panel();
            line.Left = 0;
            line.Top = 564;
            line.Width = 1024;
            line.Height = 1;
            line.BackColor = border;
            root.Controls.Add(line);

            statusLabel.Text = "Pronto para iniciar";
            statusLabel.Left = 62;
            statusLabel.Top = 590;
            statusLabel.Width = 300;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.FromArgb(38, 48, 64);
            statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(statusLabel);

            InfoCircle info = new InfoCircle();
            info.Left = 40;
            info.Top = 588;
            info.Width = 18;
            info.Height = 18;
            info.ForeColor = blue;
            root.Controls.Add(info);

            keepOpenWhenDoneCheckBox.Text = "Manter aberto ao finalizar";
            keepOpenWhenDoneCheckBox.Left = 384;
            keepOpenWhenDoneCheckBox.Top = 584;
            keepOpenWhenDoneCheckBox.Width = 205;
            keepOpenWhenDoneCheckBox.Height = 24;
            keepOpenWhenDoneCheckBox.Checked = true;
            keepOpenWhenDoneCheckBox.ForeColor = Color.FromArgb(38, 48, 64);
            keepOpenWhenDoneCheckBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(keepOpenWhenDoneCheckBox);

            installButton.Text = "Instalar";
            installButton.Left = 604;
            installButton.Top = 572;
            installButton.Width = 150;
            installButton.Height = 40;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            installButton.BackColor = Color.FromArgb(0, 104, 210);
            installButton.ForeColor = Color.White;
            installButton.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            installButton.Click += delegate { StartInstall(); };
            root.Controls.Add(installButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 772;
            cancelButton.Top = 572;
            cancelButton.Width = 124;
            cancelButton.Height = 40;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelInstall(); };
            root.Controls.Add(cancelButton);

            closeButton.Text = "Fechar";
            closeButton.Left = 912;
            closeButton.Top = 572;
            closeButton.Width = 88;
            closeButton.Height = 40;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = border;
            closeButton.BackColor = Color.FromArgb(244, 246, 249);
            closeButton.Enabled = true;
            closeButton.Click += delegate { Close(); };
            root.Controls.Add(closeButton);
        }

        private void SelectMode(InstallMode mode)
        {
            for (int i = 0; i < optionRows.Count; i++)
            {
                optionRows[i].SetSelected(optionRows[i].Mode == mode);
            }

            UpdateProfileControls();
        }

        private InstallMode SelectedMode
        {
            get
            {
                for (int i = 0; i < optionRows.Count; i++)
                {
                    if (optionRows[i].IsSelected)
                    {
                        return optionRows[i].Mode;
                    }
                }

                return InstallMode.Versao;
            }
        }

        private void UpdateProfileControls()
        {
            InstallMode mode = SelectedMode;
            bool needsVersion = mode == InstallMode.Versao ||
                mode == InstallMode.Full ||
                mode == InstallMode.SemiFull ||
                (mode == InstallMode.TekFarma && servidorRadio.Checked);
            bool needsProfile = mode == InstallMode.TekFarma;

            normalRadio.Enabled = needsVersion;
            iRadio.Enabled = needsVersion;
            servidorRadio.Enabled = needsProfile;
            terminalRadio.Enabled = needsProfile;
        }

        private void InstallerForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            cancelRequested = true;
            KillRunningProcessTree();
        }

        private void StartInstall()
        {
            if (worker != null && worker.IsBusy)
            {
                return;
            }

            cancelRequested = false;
            progressBar.Value = 0;
            progressLabel.Text = "0% concluido";
            logBox.Clear();
            statusLabel.Text = "Preparando instalacao";
            currentStepLabel.Text = "Preparando arquivos...";
            installButton.Enabled = false;
            cancelButton.Enabled = true;
            closeButton.Enabled = false;
            SetInputsEnabled(false);

            WorkPlan plan = BuildWorkPlan();

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object sender, DoWorkEventArgs e)
            {
                ExecutePlan(plan, (BackgroundWorker)sender);
            };
            worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e)
            {
                SetProgress(e.ProgressPercentage);
                if (e.UserState != null)
                {
                    currentStepLabel.Text = e.UserState.ToString();
                }
            };
            worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
            {
                runningProcess = null;
                cancelButton.Enabled = false;
                closeButton.Enabled = true;
                installButton.Enabled = true;
                SetInputsEnabled(true);

                if (cancelRequested)
                {
                    SetProgress(0);
                    currentStepLabel.Text = "Instalacao cancelada";
                    statusLabel.Text = "Cancelado pelo usuario";
                    AppendLog("[AVISO] Instalacao cancelada.");
                    return;
                }

                if (e.Error != null)
                {
                    currentStepLabel.Text = "Instalacao finalizada com erro";
                    statusLabel.Text = "Erro durante a instalacao";
                    AppendLog("[ERRO] " + e.Error.Message);
                    return;
                }

                SetProgress(100);
                currentStepLabel.Text = "Instalacao finalizada";
                statusLabel.Text = "Processo concluido";
                AppendLog("[OK] Processo finalizado.");

                if (!keepOpenWhenDoneCheckBox.Checked)
                {
                    BeginInvoke(new Action(Close));
                }
            };
            worker.RunWorkerAsync();
        }

        private void CancelInstall()
        {
            cancelRequested = true;
            statusLabel.Text = "Cancelando...";
            AppendLog("[AVISO] Cancelamento solicitado.");
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

        private WorkPlan BuildWorkPlan()
        {
            InstallMode mode = SelectedMode;
            string tipoVersao = normalRadio.Checked ? "normal" : "i";
            string perfilTek = servidorRadio.Checked ? "servidor" : "terminal";

            if (mode == InstallMode.TekFarma && perfilTek == "terminal")
            {
                tipoVersao = "normal";
            }

            WorkPlan plan = new WorkPlan();
            plan.Mode = mode;
            plan.TipoVersao = tipoVersao;
            plan.PerfilTek = perfilTek;

            if (mode == InstallMode.Crystal || mode == InstallMode.Full || mode == InstallMode.SemiFull || mode == InstallMode.TekFarma)
            {
                AddRelease(plan, "CRRuntime_32bit_13_0_39.msi");
                AddRelease(plan, "crdb_adoplus.zip");
            }

            if (mode == InstallMode.Full || mode == InstallMode.TekFarma)
            {
                AddRelease(plan, "dotnet48.exe");
                AddRelease(plan, "VC_redist.x86.exe");
                AddRelease(plan, "VC_redist.x64.exe");
            }

            if (mode == InstallMode.TekFarma && perfilTek == "servidor")
            {
                AddRelease(plan, "Firebird-2.5.9.exe");
                AddRelease(plan, "TekFarmaPasta.zip");
                AddRelease(plan, "pastastekfarma.zip");
                AddRelease(plan, "DLLS.zip");
            }

            if (mode == InstallMode.Versao || mode == InstallMode.Full || mode == InstallMode.SemiFull || (mode == InstallMode.TekFarma && perfilTek == "servidor"))
            {
                if (tipoVersao == "normal")
                {
                    plan.Downloads.Add(new DownloadItem(UrlVersaoNormal, "TekFarma50.exe", "TekFarma50.exe"));
                }
                else
                {
                    plan.Downloads.Add(new DownloadItem(UrlVersaoI, "TekFarma50i.exe", "TekFarma50i.exe"));
                }
            }

            if (mode == InstallMode.TekFarma && perfilTek == "servidor")
            {
                plan.Downloads.Add(new DownloadItem(UrlBancoTekFarma, "TEKFARMA(NOV-2020).zip", "TEKFARMA(NOV-2020).zip"));
            }

            if (mode == InstallMode.TekFarma)
            {
                plan.ScriptName = "instalar_tekfarma.ps1";
                plan.Downloads.Add(new DownloadItem(RawUrl + "/instalar_tekfarma.ps1", "instalar_tekfarma.ps1", "instalar_tekfarma.ps1"));
                plan.ScriptArguments = "-TipoVersao \"" + tipoVersao + "\" -PerfilTek \"" + perfilTek + "\"";
            }
            else
            {
                plan.ScriptName = "instalar.ps1";
                plan.Downloads.Add(new DownloadItem(RawUrl + "/instalar.ps1", "instalar.ps1", "instalar.ps1"));
                plan.ScriptArguments = "-Modo " + ModeToNumber(mode) + " -TipoVersao \"" + tipoVersao + "\"";
            }

            return plan;
        }

        private void AddRelease(WorkPlan plan, string fileName)
        {
            plan.Downloads.Add(new DownloadItem(BaseUrl + "/" + fileName, fileName, fileName));
        }

        private string ModeToNumber(InstallMode mode)
        {
            if (mode == InstallMode.Versao) return "1";
            if (mode == InstallMode.Crystal) return "2";
            if (mode == InstallMode.Full) return "3";
            if (mode == InstallMode.SemiFull) return "4";
            return "1";
        }

        private void ExecutePlan(WorkPlan plan, BackgroundWorker bg)
        {
            tempDir = Path.Combine(Path.GetTempPath(), "InstalacaoCrystal");

            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }

            Directory.CreateDirectory(tempDir);

            int totalUnits = Math.Max(1, plan.Downloads.Count + 1);
            int done = 0;

            AppendLog("[INFO] Pasta temporaria: " + tempDir);

            for (int i = 0; i < plan.Downloads.Count; i++)
            {
                if (cancelRequested) return;

                DownloadItem item = plan.Downloads[i];
                string destination = Path.Combine(tempDir, item.FileName);

                bg.ReportProgress(CalcPercent(done, totalUnits), "Baixando " + item.Name + "...");
                AppendLog("[INFO] Baixando: " + item.Name);
                AppendLog("[URL] " + item.Url);

                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(item.Url, destination);
                }

                FileInfo fi = new FileInfo(destination);
                AppendLog("[OK] " + item.Name + " baixado (" + FormatBytes(fi.Length) + ")");
                done++;
                bg.ReportProgress(CalcPercent(done, totalUnits), "Download concluido: " + item.Name);
            }

            if (cancelRequested) return;

            bg.ReportProgress(CalcPercent(done, totalUnits), "Executando instalador PowerShell...");
            string scriptPath = Path.Combine(tempDir, plan.ScriptName);
            string args = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + plan.ScriptArguments;

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
                        try
                        {
                            KillRunningProcessTree();
                        }
                        catch
                        {
                        }

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

            done++;
            bg.ReportProgress(CalcPercent(done, totalUnits), "Instalacao concluida");
        }

        private int CalcPercent(int done, int total)
        {
            int value = (int)Math.Round((done * 100.0) / total);
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

        private void SetProgress(int value)
        {
            if (value < progressBar.Minimum) value = progressBar.Minimum;
            if (value > progressBar.Maximum) value = progressBar.Maximum;

            progressBar.Value = value;
            progressLabel.Text = value + "% concluido";
        }

        private void AppendLog(string text)
        {
            if (logBox.InvokeRequired)
            {
                logBox.BeginInvoke(new Action<string>(AppendLog), text);
                return;
            }

            logBox.AppendText(text + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void SetInputsEnabled(bool enabled)
        {
            for (int i = 0; i < optionRows.Count; i++)
            {
                optionRows[i].Enabled = enabled;
            }

            keepOpenWhenDoneCheckBox.Enabled = enabled;

            if (!enabled)
            {
                normalRadio.Enabled = false;
                iRadio.Enabled = false;
                servidorRadio.Enabled = false;
                terminalRadio.Enabled = false;
            }
            else
            {
                UpdateProfileControls();
            }
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

    internal enum InstallMode
    {
        Versao,
        Crystal,
        Full,
        SemiFull,
        TekFarma
    }

    internal sealed class WorkPlan
    {
        public InstallMode Mode;
        public string TipoVersao;
        public string PerfilTek;
        public string ScriptName;
        public string ScriptArguments;
        public readonly List<DownloadItem> Downloads = new List<DownloadItem>();
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

    internal sealed class OptionRow : Panel
    {
        private readonly RadioButton radio = new RadioButton();
        private readonly Label titleLabel = new Label();
        private readonly Label subtitleLabel = new Label();
        private readonly string iconKind;
        private bool selected;

        public event EventHandler SelectedChanged;
        public readonly InstallMode Mode;

        public OptionRow(InstallMode mode, string title, string subtitle, string icon)
        {
            Mode = mode;
            iconKind = icon;
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            radio.Left = 10;
            radio.Top = 17;
            radio.Width = 24;
            radio.Height = 24;
            radio.CheckedChanged += delegate
            {
                if (radio.Checked && SelectedChanged != null)
                {
                    SelectedChanged(this, EventArgs.Empty);
                }
            };
            Controls.Add(radio);

            titleLabel.Text = title;
            titleLabel.Left = 90;
            titleLabel.Top = String.IsNullOrEmpty(subtitle) ? 16 : 9;
            titleLabel.Width = 210;
            titleLabel.Height = 24;
            titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.FromArgb(28, 36, 48);
            titleLabel.Click += delegate { OnClick(EventArgs.Empty); };
            Controls.Add(titleLabel);

            subtitleLabel.Text = subtitle;
            subtitleLabel.Left = 90;
            subtitleLabel.Top = 29;
            subtitleLabel.Width = 210;
            subtitleLabel.Height = 22;
            subtitleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.ForeColor = Color.FromArgb(28, 36, 48);
            subtitleLabel.Visible = !String.IsNullOrEmpty(subtitle);
            subtitleLabel.Click += delegate { OnClick(EventArgs.Empty); };
            Controls.Add(subtitleLabel);

            Click += delegate
            {
                if (SelectedChanged != null)
                {
                    SelectedChanged(this, EventArgs.Empty);
                }
            };
        }

        public bool IsSelected
        {
            get { return selected; }
        }

        public void SetSelected(bool value)
        {
            selected = value;
            radio.Checked = value;
            BackColor = value ? Color.FromArgb(232, 244, 255) : Color.White;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            if (selected)
            {
                using (Pen p = new Pen(Color.FromArgb(54, 140, 230)))
                {
                    e.Graphics.DrawRectangle(p, rect);
                }
            }

            DrawIcon(e.Graphics, new Rectangle(52, 12, 30, 30), iconKind);
        }

        private void DrawIcon(Graphics g, Rectangle r, string kind)
        {
            Color blue = Color.FromArgb(0, 92, 190);
            using (Brush b = new SolidBrush(blue))
            using (Pen p = new Pen(blue, 3F))
            {
                if (kind == "diamond")
                {
                    Point[] pts = new Point[] {
                        new Point(r.Left + r.Width / 2, r.Top),
                        new Point(r.Right, r.Top + r.Height / 2),
                        new Point(r.Left + r.Width / 2, r.Bottom),
                        new Point(r.Left, r.Top + r.Height / 2)
                    };
                    g.FillPolygon(b, pts);
                    using (Pen white = new Pen(Color.White, 1.5F))
                    {
                        g.DrawLine(white, r.Left + r.Width / 2, r.Top + 3, r.Left + r.Width / 2, r.Bottom - 3);
                    }
                }
                else if (kind == "stack")
                {
                    Point[] p1 = new Point[] { new Point(r.Left, r.Top + 9), new Point(r.Left + r.Width / 2, r.Top), new Point(r.Right, r.Top + 9), new Point(r.Left + r.Width / 2, r.Top + 18) };
                    Point[] p2 = new Point[] { new Point(r.Left, r.Top + 18), new Point(r.Left + r.Width / 2, r.Top + 27), new Point(r.Right, r.Top + 18) };
                    g.DrawPolygon(p, p1);
                    g.DrawLines(p, p2);
                    g.DrawLine(p, r.Left, r.Top + 26, r.Left + r.Width / 2, r.Bottom);
                    g.DrawLine(p, r.Right, r.Top + 26, r.Left + r.Width / 2, r.Bottom);
                }
                else if (kind == "network")
                {
                    g.FillRectangle(b, r.Left + 7, r.Top, 16, 10);
                    g.DrawLine(p, r.Left + 15, r.Top + 10, r.Left + 15, r.Top + 22);
                    g.DrawLine(p, r.Left + 4, r.Top + 22, r.Right - 4, r.Top + 22);
                    g.FillRectangle(b, r.Left, r.Bottom - 8, 10, 8);
                    g.FillRectangle(b, r.Left + 20, r.Bottom - 8, 10, 8);
                }
                else
                {
                    Point[] top = new Point[] { new Point(r.Left + 15, r.Top), new Point(r.Right, r.Top + 8), new Point(r.Left + 15, r.Top + 16), new Point(r.Left, r.Top + 8) };
                    Point[] left = new Point[] { new Point(r.Left, r.Top + 8), new Point(r.Left + 15, r.Top + 16), new Point(r.Left + 15, r.Bottom), new Point(r.Left, r.Bottom - 8) };
                    Point[] right = new Point[] { new Point(r.Right, r.Top + 8), new Point(r.Left + 15, r.Top + 16), new Point(r.Left + 15, r.Bottom), new Point(r.Right, r.Bottom - 8) };
                    g.FillPolygon(b, top);
                    using (Brush b2 = new SolidBrush(Color.FromArgb(0, 112, 225)))
                    {
                        g.FillPolygon(b2, left);
                    }
                    using (Brush b3 = new SolidBrush(Color.FromArgb(0, 74, 165)))
                    {
                        g.FillPolygon(b3, right);
                    }
                }
            }
        }
    }

    internal sealed class GearPanel : Panel
    {
        public GearPanel()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int cx = Width / 2;
            int cy = Height / 2;
            using (Brush b = new SolidBrush(ForeColor))
            {
                e.Graphics.FillEllipse(b, 6, 6, Width - 12, Height - 12);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    int x = cx + (int)(Math.Cos(a) * 13) - 3;
                    int y = cy + (int)(Math.Sin(a) * 13) - 3;
                    e.Graphics.FillRectangle(b, x, y, 6, 6);
                }
                using (Brush w = new SolidBrush(Color.White))
                {
                    e.Graphics.FillEllipse(w, cx - 5, cy - 5, 10, 10);
                }
            }
        }
    }

    internal sealed class InfoCircle : Panel
    {
        public InfoCircle()
        {
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(ForeColor, 2F))
            using (Brush b = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawEllipse(p, 1, 1, Width - 3, Height - 3);
                e.Graphics.FillRectangle(b, Width / 2 - 1, 7, 2, Height - 9);
                e.Graphics.FillEllipse(b, Width / 2 - 1, 4, 2, 2);
            }
        }
    }
}
