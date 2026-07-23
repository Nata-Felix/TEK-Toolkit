using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Microsoft.Win32;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("TekFarmaInstaller")]
[assembly: AssemblyProduct("TEK Toolkit")]
[assembly: AssemblyCompany("SOLPPE")]
[assembly: AssemblyVersion("1.0.12.0")]
[assembly: AssemblyFileVersion("1.0.12.0")]

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
        private const string Repo = "Nata-Felix/TEK-Toolkit";
        private const string BaseUrl = "https://github.com/" + Repo + "/releases/download/" + Version;
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repo + "/refs/heads/main";
        private const string UrlVersaoNormal = BaseUrl + "/TekFarma50v109.7.zip";
        private const string UrlVersaoI = BaseUrl + "/TekFarma50v109.7i.zip";
        private const string UrlBancoTekFarma = "https://files.tekfarma.com.br/util/TEKFARMA(NOV-2020).zip";
        private const string UrlTekSync = "https://files.tekfarma.com.br/versao/TekSync%201.10.0.zip";

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
        private readonly CheckBox repairWithoutCrystalCheckBox = new CheckBox();
        private readonly CheckBox verifyBeforeDownloadCheckBox = new CheckBox();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly RichTextBox logBox = new RichTextBox();
        private readonly Button installButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly Button copyLogButton = new Button();
        private readonly CheckBox closeWhenDoneCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly PictureBox logoBox = new PictureBox();
        private readonly ToolTip optionToolTip = new ToolTip();

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
            optionToolTip.AutoPopDelay = 12000;
            optionToolTip.InitialDelay = 350;
            optionToolTip.ReshowDelay = 100;
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

            AddOption(optionsPanel, InstallMode.Versao, 4, "Somente Versao", "", "box",
                "Atualiza apenas a versao do TekFarma em C:\\TekSoftware\\TekFarma.");
            AddOption(optionsPanel, InstallMode.TekSync, 54, "Atualizar TekSync", "1.10.0", "sync",
                "Atualiza o TekSync 1.10.0 no servidor e altera somente a chave Autenticador do sync.ini.");
            AddOption(optionsPanel, InstallMode.Crystal, 104, "Somente Crystal", "", "diamond",
                ".NET 4.8, VS x86/x64, CRRuntime_39 e fix Crystal.");
            AddOption(optionsPanel, InstallMode.CrystalWin7, 154, "Somente Crystal Win7", "", "diamond",
                ".NET 4.8, VS x86 para Windows 7, CRRuntime_39 e fix Crystal.");
            AddOption(optionsPanel, InstallMode.Full, 204, "Completo", "versao + Crystal", "stack",
                "Atualiza a versao do TekFarma e instala .NET 4.8, VS x86/x64, CRRuntime_39 e fix Crystal.");
            AddOption(optionsPanel, InstallMode.TekFarma, 254, "Novo Servidor/Terminal", "", "network",
                "Servidor: Firebird, pastas, banco, versao e dependencias. Terminal: dependencias, credencial, mapeamento e atalho.");

            BuildChoicePanel(root);
            BuildProgressPanel(root);
        }

        private void AddOption(Panel parent, InstallMode mode, int top, string title, string subtitle, string icon, string tooltip)
        {
            OptionRow row = new OptionRow(mode, title, subtitle, icon);
            row.Left = 8;
            row.Top = top;
            row.Width = 320;
            row.Height = 48;
            row.SelectedChanged += delegate { SelectMode(row.Mode); };
            parent.Controls.Add(row);
            optionRows.Add(row);
            SetTooltip(row, tooltip);
        }

        private void SetTooltip(Control control, string text)
        {
            optionToolTip.SetToolTip(control, text);

            foreach (Control child in control.Controls)
            {
                SetTooltip(child, text);
            }
        }

        private void BuildChoicePanel(Control root)
        {
            Panel choicePanel = new Panel();
            choicePanel.Left = 42;
            choicePanel.Top = 510;
            choicePanel.Width = 358;
            choicePanel.Height = 54;
            choicePanel.BackColor = Color.White;
            root.Controls.Add(choicePanel);

            Panel versionPanel = new Panel();
            versionPanel.Left = 0;
            versionPanel.Top = 0;
            versionPanel.Width = 125;
            versionPanel.Height = 54;
            versionPanel.BackColor = Color.White;
            choicePanel.Controls.Add(versionPanel);

            Panel profilePanel = new Panel();
            profilePanel.Left = 132;
            profilePanel.Top = 0;
            profilePanel.Width = 95;
            profilePanel.Height = 54;
            profilePanel.BackColor = Color.White;
            choicePanel.Controls.Add(profilePanel);

            repairWithoutCrystalCheckBox.Text = "Reparo sem\nCrystal";
            repairWithoutCrystalCheckBox.Left = 232;
            repairWithoutCrystalCheckBox.Top = 0;
            repairWithoutCrystalCheckBox.Width = 124;
            repairWithoutCrystalCheckBox.Height = 27;
            repairWithoutCrystalCheckBox.Enabled = false;
            choicePanel.Controls.Add(repairWithoutCrystalCheckBox);
            optionToolTip.SetToolTip(repairWithoutCrystalCheckBox, "Instala .NET, Visual C++ e aplica o fix sem remover ou instalar o Crystal Runtime.");

            verifyBeforeDownloadCheckBox.Text = "Verificar antes\nde baixar";
            verifyBeforeDownloadCheckBox.Left = 232;
            verifyBeforeDownloadCheckBox.Top = 27;
            verifyBeforeDownloadCheckBox.Width = 124;
            verifyBeforeDownloadCheckBox.Height = 27;
            verifyBeforeDownloadCheckBox.Checked = false;
            choicePanel.Controls.Add(verifyBeforeDownloadCheckBox);
            optionToolTip.SetToolTip(verifyBeforeDownloadCheckBox, "Verifica .NET Framework 4.8 e Visual C++ instalados e baixa somente o que estiver faltando.");

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
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            logBox.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.BackColor = Color.White;
            logBox.ForeColor = Color.FromArgb(24, 30, 38);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(logBox);

            copyLogButton.Text = "Copiar log";
            copyLogButton.Left = 862;
            copyLogButton.Top = 286;
            copyLogButton.Width = 118;
            copyLogButton.Height = 30;
            copyLogButton.FlatStyle = FlatStyle.Flat;
            copyLogButton.FlatAppearance.BorderColor = border;
            copyLogButton.BackColor = Color.White;
            copyLogButton.ForeColor = Color.FromArgb(180, 32, 32);
            copyLogButton.Enabled = false;
            copyLogButton.Visible = false;
            copyLogButton.Click += delegate { CopyErrorLog(); };
            root.Controls.Add(copyLogButton);
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

            closeWhenDoneCheckBox.Text = "Fechar automaticamente ao finalizar";
            closeWhenDoneCheckBox.Left = 364;
            closeWhenDoneCheckBox.Top = 584;
            closeWhenDoneCheckBox.Width = 225;
            closeWhenDoneCheckBox.Height = 24;
            closeWhenDoneCheckBox.Checked = false;
            closeWhenDoneCheckBox.ForeColor = Color.FromArgb(38, 48, 64);
            closeWhenDoneCheckBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(closeWhenDoneCheckBox);

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
                (mode == InstallMode.TekFarma && servidorRadio.Checked);
            bool needsProfile = mode == InstallMode.TekFarma;

            normalRadio.Enabled = needsVersion;
            iRadio.Enabled = needsVersion;
            servidorRadio.Enabled = needsProfile;
            terminalRadio.Enabled = needsProfile;
            repairWithoutCrystalCheckBox.Enabled = mode == InstallMode.Crystal;
            if (mode != InstallMode.Crystal) repairWithoutCrystalCheckBox.Checked = false;
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
            copyLogButton.Enabled = false;
            copyLogButton.Visible = false;
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

                if (plan.Mode == InstallMode.TekSync)
                {
                    currentStepLabel.Text = "TekSync iniciado";
                    statusLabel.Text = "Confirme a sincronizacao no TekSync";
                    AppendLog("[AVISO] Verifique na tela do TekSync se a sincronizacao terminou sem erros.");
                    AppendLog("[INFO] A janela permanecera aberta para esta confirmacao.");
                    return;
                }

                currentStepLabel.Text = "Instalacao finalizada";
                statusLabel.Text = "Processo concluido";
                AppendLog("[OK] Processo finalizado.");

                if (closeWhenDoneCheckBox.Checked)
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
            plan.VerificarAntesDeBaixar = verifyBeforeDownloadCheckBox.Checked;
            bool repairWithoutCrystal = mode == InstallMode.Crystal && repairWithoutCrystalCheckBox.Checked;
            bool windows7Compatibility = mode == InstallMode.CrystalWin7 || IsWindows7();

            if (mode == InstallMode.TekSync)
            {
                plan.Downloads.Add(new DownloadItem(UrlTekSync, "TekSync 1.10.0.zip", "TekSync 1.10.0.zip"));
            }

            if ((mode == InstallMode.Crystal && !repairWithoutCrystal) || mode == InstallMode.CrystalWin7 || mode == InstallMode.Full || mode == InstallMode.TekFarma)
            {
                AddRelease(plan, "CRRuntime_32bit_13_0_39.msi");
            }

            if (mode == InstallMode.Crystal || mode == InstallMode.CrystalWin7 || mode == InstallMode.Full || mode == InstallMode.TekFarma)
            {
                AddRelease(plan, "crdb_adoplus.zip");
            }

            if (mode == InstallMode.Crystal || mode == InstallMode.CrystalWin7 || mode == InstallMode.Full || mode == InstallMode.TekFarma)
            {
                AddRelease(plan, "dotnet48.exe");

                if (windows7Compatibility)
                {
                    AddRelease(plan, "VC_redist.x86.Win7.exe");
                    if (Environment.Is64BitOperatingSystem)
                    {
                        AddRelease(plan, "Windows6.1-KB2999226-x64.msu");
                    }
                    else
                    {
                        AddRelease(plan, "Windows6.1-KB2999226-x86.msu");
                    }
                }
                else
                {
                    AddRelease(plan, "VC_redist.x86.exe");
                    AddRelease(plan, "VC_redist.x64.exe");
                }
            }

            if (mode == InstallMode.TekFarma && perfilTek == "servidor")
            {
                AddRelease(plan, "Firebird-2.5.9.exe");
                AddRelease(plan, "TekFarmaPasta.zip");
                AddRelease(plan, "pastastekfarma.zip");
                AddRelease(plan, "DLLS.zip");
            }

            if (mode == InstallMode.Versao || mode == InstallMode.Full || (mode == InstallMode.TekFarma && perfilTek == "servidor"))
            {
                if (tipoVersao == "normal")
                {
                    plan.Downloads.Add(new DownloadItem(UrlVersaoNormal, "TekFarma50v109.7.zip", "TekFarma 1.09.7"));
                }
                else
                {
                    plan.Downloads.Add(new DownloadItem(UrlVersaoI, "TekFarma50v109.7i.zip", "TekFarma 1.09.7i"));
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
                plan.ScriptArguments = "-TipoVersao \"" + tipoVersao + "\" -PerfilTek \"" + perfilTek + "\" -CompatibilidadeWin7 \"" + (windows7Compatibility ? "true" : "false") + "\" -VerificarAntesDeBaixar \"" + (plan.VerificarAntesDeBaixar ? "true" : "false") + "\"";
            }
            else
            {
                plan.ScriptName = "instalar.ps1";
                plan.Downloads.Add(new DownloadItem(RawUrl + "/instalar.ps1", "instalar.ps1", "instalar.ps1"));
                plan.ScriptArguments = "-Modo " + ModeToNumber(mode) + " -TipoVersao \"" + tipoVersao + "\" -ReparoSemCrystal \"" + (repairWithoutCrystal ? "true" : "false") + "\" -CompatibilidadeWin7 \"" + (windows7Compatibility ? "true" : "false") + "\" -VerificarAntesDeBaixar \"" + (plan.VerificarAntesDeBaixar ? "true" : "false") + "\"";
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
            if (mode == InstallMode.CrystalWin7) return "4";
            if (mode == InstallMode.TekSync) return "5";
            return "1";
        }

        private static bool IsWindows7()
        {
            Version version = Environment.OSVersion.Version;
            return version.Major == 6 && version.Minor == 1;
        }

        private TimeSpan GetDownloadCacheAge(DownloadItem item)
        {
            if (item == null || item.FileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return TimeSpan.Zero;
            }

            if (item.Url.IndexOf("/releases/download/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TimeSpan.FromDays(30);
            }

            return TimeSpan.FromHours(2);
        }

        private bool IsDownloadFileValid(string path, DownloadItem item, TimeSpan maxAge)
        {
            try
            {
                FileInfo file = new FileInfo(path);
                if (!file.Exists || file.Length < 128)
                {
                    return false;
                }

                if (maxAge > TimeSpan.Zero && DateTime.UtcNow - file.LastWriteTimeUtc > maxAge)
                {
                    return false;
                }

                string extension = Path.GetExtension(item.FileName).ToLowerInvariant();
                byte[] header = new byte[8];
                int read;

                using (FileStream stream = File.OpenRead(path))
                {
                    read = stream.Read(header, 0, header.Length);
                }

                if (extension == ".exe")
                {
                    bool isExecutable = read >= 2 && header[0] == 0x4D && header[1] == 0x5A;
                    bool isZipPayload = read >= 4 && header[0] == 0x50 && header[1] == 0x4B;
                    return file.Length >= 65536 && (isExecutable || isZipPayload);
                }

                if (extension == ".zip")
                {
                    return read >= 4 && header[0] == 0x50 && header[1] == 0x4B;
                }

                if (extension == ".msi")
                {
                    return read >= 8 &&
                        header[0] == 0xD0 && header[1] == 0xCF &&
                        header[2] == 0x11 && header[3] == 0xE0 &&
                        header[4] == 0xA1 && header[5] == 0xB1 &&
                        header[6] == 0x1A && header[7] == 0xE1;
                }

                if (extension == ".ps1")
                {
                    string prefix;
                    using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
                    {
                        char[] chars = new char[256];
                        int charsRead = reader.Read(chars, 0, chars.Length);
                        prefix = new string(chars, 0, charsRead);
                    }

                    return file.Length >= 512 &&
                        prefix.IndexOf("<html", StringComparison.OrdinalIgnoreCase) < 0 &&
                        prefix.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) < 0;
                }

                return file.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private string FindReusableDownload(DownloadItem item, string cacheDir, TimeSpan maxAge)
        {
            if (maxAge <= TimeSpan.Zero)
            {
                return null;
            }

            string cachePath = Path.Combine(cacheDir, item.FileName);
            if (IsDownloadFileValid(cachePath, item, maxAge))
            {
                return cachePath;
            }

            string bestCandidate = null;
            DateTime bestWriteTime = DateTime.MinValue;

            try
            {
                string[] previousRuns = Directory.GetDirectories(Path.GetTempPath(), "InstalacaoCrystal_*");
                for (int i = 0; i < previousRuns.Length; i++)
                {
                    if (String.Equals(previousRuns[i], tempDir, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string candidate = Path.Combine(previousRuns[i], item.FileName);
                    if (!IsDownloadFileValid(candidate, item, maxAge))
                    {
                        continue;
                    }

                    DateTime writeTime = File.GetLastWriteTimeUtc(candidate);
                    if (writeTime > bestWriteTime)
                    {
                        bestWriteTime = writeTime;
                        bestCandidate = candidate;
                    }
                }
            }
            catch
            {
            }

            if (String.IsNullOrWhiteSpace(bestCandidate))
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(cacheDir);
                File.Copy(bestCandidate, cachePath, true);
                return cachePath;
            }
            catch
            {
                return bestCandidate;
            }
        }

        private bool DownloadOrReuseFile(DownloadItem item, string destination, string cacheDir, BackgroundWorker bg, int completedUnits, int totalUnits)
        {
            TimeSpan maxAge = GetDownloadCacheAge(item);
            string reusable = FindReusableDownload(item, cacheDir, maxAge);

            if (!String.IsNullOrWhiteSpace(reusable) && !IsKnownPackageHashValid(reusable, item))
            {
                AppendLog("[CACHE] SHA-256 invalido; descartando cache de " + item.Name + ".");
                reusable = null;
            }

            if (!String.IsNullOrWhiteSpace(reusable))
            {
                File.Copy(reusable, destination, true);
                AppendLog("[CACHE] Reutilizado: " + item.Name);
                AppendLog("[CACHE] Origem: " + reusable);
                return true;
            }

            string cachePath = maxAge > TimeSpan.Zero ? Path.Combine(cacheDir, item.FileName) : null;
            string partialBase = cachePath ?? destination;
            string partialPath = partialBase + ".partial." + Process.GetCurrentProcess().Id + "." + Guid.NewGuid().ToString("N");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(partialPath));

                Exception lastError = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        if (File.Exists(partialPath)) File.Delete(partialPath);
                        AppendLog("[DOWNLOAD] " + item.Name + " - tentativa " + attempt + " de 3.");
                        DownloadFileWithVisualProgress(item, partialPath, bg, completedUnits, totalUnits);
                        lastError = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (cancelRequested) throw;
                        lastError = ex;
                        AppendLog("[AVISO] Tentativa " + attempt + " falhou: " + ex.Message);
                        if (attempt < 3) Thread.Sleep(1500);
                    }
                }

                if (lastError != null)
                    throw new InvalidOperationException("Nao foi possivel baixar " + item.Name + " apos 3 tentativas. " + lastError.Message, lastError);

                if (!IsDownloadFileValid(partialPath, item, TimeSpan.Zero))
                {
                    throw new InvalidDataException(item.Name + " foi baixado incompleto ou em formato invalido.");
                }

                if (!IsKnownPackageHashValid(partialPath, item))
                {
                    string recebido = CalculateSha256(partialPath);
                    throw new InvalidDataException(
                        item.Name + " falhou na verificacao SHA-256. Esperado: " +
                        GetExpectedPackageHash(item) + "; recebido: " + recebido + ".");
                }

                if (cachePath != null)
                {
                    if (File.Exists(cachePath))
                    {
                        File.Delete(cachePath);
                    }

                    File.Move(partialPath, cachePath);
                    File.Copy(cachePath, destination, true);
                }
                else
                {
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    File.Move(partialPath, destination);
                }

                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(partialPath))
                    {
                        File.Delete(partialPath);
                    }
                }
                catch
                {
                }
            }
        }

        private bool IsKnownPackageHashValid(string path, DownloadItem item)
        {
            string expected = GetExpectedPackageHash(item);
            if (String.IsNullOrWhiteSpace(expected)) return true;
            if (!File.Exists(path)) return false;

            try
            {
                return String.Equals(CalculateSha256(path), expected, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string GetExpectedPackageHash(DownloadItem item)
        {
            if (item != null && String.Equals(item.FileName, "TekSync 1.10.0.zip", StringComparison.OrdinalIgnoreCase))
            {
                return "BDD253CD129108CEC1813DD1AA3C13EF3AD92F26D04813B494E51ECC971ABC9A";
            }

            return null;
        }

        private string CalculateSha256(string path)
        {
            using (SHA256 sha = new SHA256Managed())
            using (FileStream stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }

        private void DownloadFileWithVisualProgress(DownloadItem item, string destination, BackgroundWorker bg, int completedUnits, int totalUnits)
        {
            Exception downloadError = null;
            bool downloadCancelled = false;

            using (ManualResetEvent finished = new ManualResetEvent(false))
            using (LongTimeoutWebClient client = new LongTimeoutWebClient())
            {
                client.Headers.Add(HttpRequestHeader.UserAgent, "TEK-Toolkit/1.0");
                client.DownloadProgressChanged += delegate(object sender, DownloadProgressChangedEventArgs e)
                {
                    int overall = CalcDownloadPercent(completedUnits, totalUnits, e.ProgressPercentage);
                    string sizeText = FormatBytes(e.BytesReceived);
                    if (e.TotalBytesToReceive > 0)
                        sizeText += " de " + FormatBytes(e.TotalBytesToReceive);

                    bg.ReportProgress(overall, "Baixando " + item.Name + ": " + e.ProgressPercentage + "% (" + sizeText + ")");
                };
                client.DownloadFileCompleted += delegate(object sender, AsyncCompletedEventArgs e)
                {
                    downloadError = e.Error;
                    downloadCancelled = e.Cancelled;
                    finished.Set();
                };

                bg.ReportProgress(CalcDownloadPercent(completedUnits, totalUnits, 0), "Baixando " + item.Name + ": 0%");
                client.DownloadFileAsync(new Uri(item.Url), destination);

                while (!finished.WaitOne(250))
                {
                    if (cancelRequested) client.CancelAsync();
                }
            }

            if (downloadCancelled || cancelRequested)
                throw new OperationCanceledException("Download cancelado pelo usuario.");
            if (downloadError != null)
                throw new InvalidOperationException(downloadError.Message, downloadError);
        }

        private int CalcDownloadPercent(int completedUnits, int totalUnits, int filePercent)
        {
            double completed = completedUnits + (Math.Max(0, Math.Min(100, filePercent)) / 100.0);
            int value = (int)Math.Round((completed * 100.0) / Math.Max(1, totalUnits));
            if (value < 0) value = 0;
            if (value > 100) value = 100;
            return value;
        }

        private void CleanupStaleCachePartials(string cacheDir)
        {
            try
            {
                if (!Directory.Exists(cacheDir))
                {
                    return;
                }

                string[] partials = Directory.GetFiles(cacheDir, "*.partial.*");
                for (int i = 0; i < partials.Length; i++)
                {
                    if (File.GetLastWriteTimeUtc(partials[i]) < DateTime.UtcNow.AddDays(-1))
                    {
                        try { File.Delete(partials[i]); } catch { }
                    }
                }
            }
            catch
            {
            }
        }

        private void ExecutePlan(WorkPlan plan, BackgroundWorker bg)
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "InstalacaoCrystal_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(tempDir);
            string cacheDir = Path.Combine(Path.GetTempPath(), "TEK-Toolkit_Cache");
            Directory.CreateDirectory(cacheDir);
            CleanupStaleCachePartials(cacheDir);

            int totalUnits = Math.Max(1, plan.Downloads.Count + 1);
            int done = 0;

            AppendLog("[INFO] Pasta temporaria: " + tempDir);
            AppendLog("[INFO] Cache de downloads: " + cacheDir);

            for (int i = 0; i < plan.Downloads.Count; i++)
            {
                if (cancelRequested) return;

                DownloadItem item = plan.Downloads[i];
                string destination = Path.Combine(tempDir, item.FileName);

                if (plan.VerificarAntesDeBaixar && DeveIgnorarDownload(item))
                {
                    AppendLog("[VERIFICACAO] " + item.Name + " ja esta instalado; download ignorado.");
                    done++;
                    bg.ReportProgress(CalcPercent(done, totalUnits), "Ja instalado: " + item.Name);
                    continue;
                }

                bg.ReportProgress(CalcPercent(done, totalUnits), "Verificando cache: " + item.Name + "...");
                AppendLog("[INFO] Verificando cache/download: " + item.Name);
                AppendLog("[URL] " + item.Url);

                bool reused = DownloadOrReuseFile(item, destination, cacheDir, bg, done, totalUnits);

                FileInfo fi = new FileInfo(destination);
                AppendLog("[OK] " + item.Name + (reused ? " reutilizado" : " baixado") + " (" + FormatBytes(fi.Length) + ")");
                done++;
                bg.ReportProgress(CalcPercent(done, totalUnits), (reused ? "Arquivo reutilizado: " : "Download concluido: ") + item.Name);
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
                bool processReportedError = false;
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
                        processReportedError = true;
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

                process.WaitForExit();

                AppendLog("[INFO] PowerShell finalizado. ExitCode: " + process.ExitCode);

                if (process.ExitCode != 0 || processReportedError)
                {
                    throw new InvalidOperationException("O script PowerShell informou erro (ExitCode " + process.ExitCode + ").");
                }
            }

            done++;
            bg.ReportProgress(CalcPercent(done, totalUnits), "Instalacao concluida");
        }

        private bool DeveIgnorarDownload(DownloadItem item)
        {
            if (item == null) return false;

            if (String.Equals(item.FileName, "dotnet48.exe", StringComparison.OrdinalIgnoreCase))
            {
                return IsDotNet48Installed();
            }

            if (String.Equals(item.FileName, "VC_redist.x86.exe", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(item.FileName, "VC_redist.x86.Win7.exe", StringComparison.OrdinalIgnoreCase))
            {
                return IsVisualCppInstalled(false);
            }

            if (String.Equals(item.FileName, "VC_redist.x64.exe", StringComparison.OrdinalIgnoreCase))
            {
                return IsVisualCppInstalled(true);
            }

            if (String.Equals(item.FileName, "Windows6.1-KB2999226-x86.msu", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(item.FileName, "Windows6.1-KB2999226-x64.msu", StringComparison.OrdinalIgnoreCase))
            {
                return IsUniversalCrtInstalled();
            }

            return false;
        }

        private bool IsUniversalCrtInstalled()
        {
            Version version = Environment.OSVersion.Version;
            if (version.Major != 6 || version.Minor != 1) return true;

            string systemDirectory = Environment.Is64BitOperatingSystem
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32");
            return File.Exists(Path.Combine(systemDirectory, "ucrtbase.dll"));
        }

        private bool IsDotNet48Installed()
        {
            string[] paths = new string[] {
                @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full",
                @"SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full"
            };

            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(paths[i]))
                    {
                        object release = key == null ? null : key.GetValue("Release");
                        if (release != null && Convert.ToInt32(release) >= 528040) return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }

        private bool IsVisualCppInstalled(bool x64)
        {
            string architecture = x64 ? "x64" : "x86";
            string[] paths = x64
                ? new string[] { @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" }
                : new string[] {
                    @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86",
                    @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"
                };

            for (int i = 0; i < paths.Length; i++)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(paths[i]))
                    {
                        object installed = key == null ? null : key.GetValue("Installed");
                        if (installed != null && Convert.ToInt32(installed) == 1)
                        {
                            AppendLog("[VERIFICACAO] Visual C++ " + architecture + " detectado no registro.");
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }

            return false;
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

            bool isError = text.IndexOf("[ERRO]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("ERRO:", StringComparison.OrdinalIgnoreCase) >= 0;

            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = isError ? Color.FromArgb(200, 30, 30) : Color.FromArgb(24, 30, 38);
            logBox.AppendText(text + Environment.NewLine);
            logBox.SelectionColor = Color.FromArgb(24, 30, 38);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();

            if (isError)
            {
                copyLogButton.Visible = true;
                copyLogButton.Enabled = true;
            }
        }

        private void CopyErrorLog()
        {
            if (String.IsNullOrWhiteSpace(logBox.Text)) return;

            try
            {
                Clipboard.SetText(logBox.Text);
                statusLabel.Text = "Log copiado para a area de transferencia";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Nao foi possivel copiar o log";
                AppendLog("[ERRO] Falha ao copiar log: " + ex.Message);
            }
        }

        private void SetInputsEnabled(bool enabled)
        {
            for (int i = 0; i < optionRows.Count; i++)
            {
                optionRows[i].Enabled = enabled;
            }

            closeWhenDoneCheckBox.Enabled = enabled;

            if (!enabled)
            {
                normalRadio.Enabled = false;
                iRadio.Enabled = false;
                servidorRadio.Enabled = false;
                terminalRadio.Enabled = false;
                repairWithoutCrystalCheckBox.Enabled = false;
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
        CrystalWin7,
        Full,
        TekFarma,
        TekSync
    }

    internal sealed class WorkPlan
    {
        public InstallMode Mode;
        public string TipoVersao;
        public string PerfilTek;
        public string ScriptName;
        public string ScriptArguments;
        public bool VerificarAntesDeBaixar;
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

    internal sealed class LongTimeoutWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 10 * 60 * 1000;

            HttpWebRequest httpRequest = request as HttpWebRequest;
            if (httpRequest != null)
            {
                httpRequest.ReadWriteTimeout = 10 * 60 * 1000;
                httpRequest.AllowAutoRedirect = true;
            }

            return request;
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
            radio.Top = 12;
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
            titleLabel.Top = String.IsNullOrEmpty(subtitle) ? 12 : 3;
            titleLabel.Width = 210;
            titleLabel.Height = 24;
            titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            titleLabel.ForeColor = Color.FromArgb(28, 36, 48);
            titleLabel.Click += delegate { OnClick(EventArgs.Empty); };
            Controls.Add(titleLabel);

            subtitleLabel.Text = subtitle;
            subtitleLabel.Left = 90;
            subtitleLabel.Top = 24;
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

            DrawIcon(e.Graphics, new Rectangle(52, 9, 30, 30), iconKind);
        }

        private void DrawIcon(Graphics g, Rectangle r, string kind)
        {
            Color blue = Color.FromArgb(0, 92, 190);
            using (Brush b = new SolidBrush(blue))
            using (Pen p = new Pen(blue, 3F))
            {
                if (kind == "sync")
                {
                    g.DrawArc(p, r.Left + 3, r.Top + 3, r.Width - 7, r.Height - 7, 35, 210);
                    g.DrawArc(p, r.Left + 3, r.Top + 3, r.Width - 7, r.Height - 7, 215, 210);
                    Point[] arrow1 = new Point[] { new Point(r.Right - 2, r.Top + 11), new Point(r.Right - 11, r.Top + 7), new Point(r.Right - 8, r.Top + 16) };
                    Point[] arrow2 = new Point[] { new Point(r.Left + 2, r.Bottom - 11), new Point(r.Left + 11, r.Bottom - 7), new Point(r.Left + 8, r.Bottom - 16) };
                    g.FillPolygon(b, arrow1);
                    g.FillPolygon(b, arrow2);
                }
                else if (kind == "diamond")
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
