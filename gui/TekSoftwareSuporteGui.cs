using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Threading;
using System.Windows.Forms;

namespace TekSoftwareSuporte
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
        private const string Version = "v1.0";
        private const string DriversVersion = "drivers-impressoras-v1";
        private const string Repo = "Nata-Felix/Instalacao_crystal_adv";
        private const string BaseUrl = "https://github.com/" + Repo + "/releases/download/" + Version;
        private const string DriversBaseUrl = "https://github.com/" + Repo + "/releases/download/" + DriversVersion;
        private const string DriversIndexUrl = DriversBaseUrl + "/drivers-impressoras.json";
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repo + "/main";

        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
        private readonly Color textMuted = Color.FromArgb(110, 119, 135);

        private readonly List<ActionOption> actionOptions = new List<ActionOption>();
        private readonly TextBox hostTextBox = new TextBox();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly Button executeButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly CheckBox closeWhenDoneCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly Label printerSelectionLabel = new Label();
        private readonly Button printerSelectButton = new Button();
        private readonly PictureBox logoBox = new PictureBox();
        private readonly ToolTip toolTip = new ToolTip();

        private BackgroundWorker worker;
        private Process runningProcess;
        private volatile bool cancelRequested;
        private string tempDir;
        private int completedUnits;
        private int totalUnits;
        private PrinterDriver selectedPrinter;
        private ActionOption printerActionOption;
        private readonly List<string> selectedPrintersToRemove = new List<string>();
        private readonly List<string> selectedPrinterDriversToRemove = new List<string>();

        public SupportForm()
        {
            Text = "Suporte TekSoftware";
            Width = 1024;
            Height = 700;
            MinimumSize = new Size(1024, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            FormClosing += SupportForm_FormClosing;

            Icon icon = LoadEmbeddedIcon("TekFarmaIcon");
            if (icon != null)
            {
                Icon = icon;
            }

            BuildLayout();
            toolTip.AutoPopDelay = 12000;
            toolTip.InitialDelay = 350;
            toolTip.ReshowDelay = 100;
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
            title.Text = "Suporte TekSoftware";
            title.Left = 380;
            title.Top = 26;
            title.Width = 520;
            title.Height = 42;
            title.Font = new Font("Segoe UI", 23F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Ferramentas de suporte e manutencao";
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
            selectLabel.Text = "Selecione as acoes de suporte";
            selectLabel.Left = 44;
            selectLabel.Top = 158;
            selectLabel.Width = 360;
            selectLabel.Height = 24;
            selectLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            selectLabel.ForeColor = blue;
            root.Controls.Add(selectLabel);

            Panel actionsPanel = new Panel();
            actionsPanel.Left = 42;
            actionsPanel.Top = 186;
            actionsPanel.Width = 336;
            actionsPanel.Height = 268;
            actionsPanel.AutoScroll = true;
            actionsPanel.BackColor = Color.White;
            actionsPanel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen p = new Pen(border))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, actionsPanel.Width - 1, actionsPanel.Height - 1);
                }
            };
            root.Controls.Add(actionsPanel);

            int y = 8;
            AddSection(actionsPanel, "Rede e acesso", ref y);
            AddAction(actionsPanel, "rede", "Configurar rede avancada", "Ativa servicos, firewall de rede, bindings e parametros de compartilhamento.", ref y);
            AddAction(actionsPanel, "credencial", "Criar credencial SERVIDOR", "Cria credencial SERVIDOR com usuario convidado e senha vazia.", ref y);
            AddAction(actionsPanel, "mapear", "Mapear TekSoftware", "Remove mapeamentos TekSoftware antigos, usa host informado e escolhe Z:, Y:, X:...", ref y);

            AddSection(actionsPanel, "Certificados", ref y);
            AddAction(actionsPanel, "certificados", "Instalar cadeia de certificado", "Baixa o zip do release e importa .cer, .sst e .p7b em Autoridades Raiz Confiaveis.", ref y);

            AddSection(actionsPanel, "Aplicativos", ref y);
            AddAction(actionsPanel, "firewall", "Adicionar excecao no firewall", "Executa os BATs e cria regras para executaveis TekSoftware encontrados.", ref y);
            AddAction(actionsPanel, "farmaciapopular", "Instalar Farmacia Popular GBAS", "Baixa o GBAS, copia para TekFarma, abre a identificacao do terminal e o portal.", ref y);

            AddSection(actionsPanel, "Servidor", ref y);
            AddAction(actionsPanel, "firebird", "Reinstalar Firebird", "Remove Firebird atual, reinstala 2.5.9 e configura recuperacao em 3 tentativas.", ref y);

            AddSection(actionsPanel, "Impressoras", ref y);
            printerActionOption = AddAction(actionsPanel, "impressora", "Instalar impressora", "Seleciona marca/modelo, baixa somente o ZIP necessario e abre o instalador.", ref y);
            printerActionOption.CheckBox.CheckedChanged += delegate
            {
                UpdatePrinterSelectionState();
                if (printerActionOption.CheckBox.Checked && selectedPrinter == null)
                {
                    if (!ShowPrinterSelectionDialog())
                    {
                        printerActionOption.CheckBox.Checked = false;
                    }
                }
            };

            AddSection(actionsPanel, "Autonomia Windows", ref y);
            AddAction(actionsPanel, "net35", "Instalar .NET 3.5", "Ativa o recurso NetFX3 pelo DISM, tentando C:\\ e depois Windows Update.", ref y);
            AddAction(actionsPanel, "net48", "Instalar .NET 4.8", "Instala o .NET Framework 4.8 offline usando o instalador do release.", ref y);
            AddAction(actionsPanel, "portacom", "Resetar portas COM", "Remove o ComDB para liberar portas COM reservadas. Pode exigir reinicio.", ref y);
            AddAction(actionsPanel, "removersenhacompartilhamento", "Remover senha de compartilhamento", "Permite uso de senha em branco em compartilhamentos locais.", ref y);
            AddAction(actionsPanel, "windowsupdatefix", "Corrigir Windows Update", "Reinicia componentes, limpa caches e registra DLLs do Windows Update.", ref y);
            AddAction(actionsPanel, "cacheicone", "Aumentar cache de icones", "Ajusta Max Cached Icons, limpa cache e reinicia o Explorer.", ref y);
            AddAction(actionsPanel, "firewalloff", "Desativar Firewall", "Desativa o Firewall do Windows em todos os perfis.", ref y);
            AddAction(actionsPanel, "resetimpressora", "Resetar impressora", "Para o spooler, limpa a fila de impressao e inicia o spooler novamente.", ref y);
            AddAction(actionsPanel, "gpedit", "Instalar GPEDIT.MSC", "Instala os pacotes GroupPolicy ClientTools e ClientExtensions via DISM.", ref y);

            BuildHostPanel(root);
            BuildPrinterPanel(root);
            BuildProgressPanel(root);
        }

        private void AddSection(Panel parent, string text, ref int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Left = 12;
            label.Top = y;
            label.Width = 280;
            label.Height = 22;
            label.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            label.ForeColor = blue;
            parent.Controls.Add(label);
            y += 26;
        }

        private ActionOption AddAction(Panel parent, string id, string title, string tooltip, ref int y)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = title;
            checkBox.Left = 18;
            checkBox.Top = y;
            checkBox.Width = 285;
            checkBox.Height = 28;
            checkBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            checkBox.ForeColor = Color.FromArgb(28, 36, 48);
            parent.Controls.Add(checkBox);
            toolTip.SetToolTip(checkBox, tooltip);
            ActionOption option = new ActionOption(id, title, checkBox);
            actionOptions.Add(option);
            y += 34;
            return option;
        }

        private void BuildHostPanel(Control root)
        {
            Label hostLabel = new Label();
            hostLabel.Text = "Host para mapeamento";
            hostLabel.Left = 44;
            hostLabel.Top = 468;
            hostLabel.Width = 190;
            hostLabel.Height = 22;
            hostLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            hostLabel.ForeColor = blue;
            root.Controls.Add(hostLabel);

            hostTextBox.Left = 42;
            hostTextBox.Top = 492;
            hostTextBox.Width = 150;
            hostTextBox.Height = 26;
            hostTextBox.Text = "SERVIDOR";
            hostTextBox.CharacterCasing = CharacterCasing.Upper;
            root.Controls.Add(hostTextBox);

            AddHostButton(root, "SERVIDOR", 202);
            AddHostButton(root, "SERVER", 262);
            AddHostButton(root, "SERVERTEK", 314);
        }

        private void BuildPrinterPanel(Control root)
        {
            Label printerLabel = new Label();
            printerLabel.Text = "Driver de impressora";
            printerLabel.Left = 44;
            printerLabel.Top = 526;
            printerLabel.Width = 190;
            printerLabel.Height = 22;
            printerLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            printerLabel.ForeColor = blue;
            root.Controls.Add(printerLabel);

            printerSelectionLabel.Text = "Nenhuma impressora selecionada";
            printerSelectionLabel.Left = 42;
            printerSelectionLabel.Top = 550;
            printerSelectionLabel.Width = 210;
            printerSelectionLabel.Height = 26;
            printerSelectionLabel.AutoEllipsis = true;
            printerSelectionLabel.ForeColor = Color.FromArgb(38, 48, 64);
            printerSelectionLabel.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(printerSelectionLabel);
            toolTip.SetToolTip(printerSelectionLabel, printerSelectionLabel.Text);

            printerSelectButton.Text = "Selecionar";
            printerSelectButton.Left = 262;
            printerSelectButton.Top = 546;
            printerSelectButton.Width = 116;
            printerSelectButton.Height = 30;
            printerSelectButton.FlatStyle = FlatStyle.Flat;
            printerSelectButton.FlatAppearance.BorderColor = border;
            printerSelectButton.BackColor = Color.White;
            printerSelectButton.Enabled = false;
            printerSelectButton.Click += delegate { ShowPrinterSelectionDialog(); };
            root.Controls.Add(printerSelectButton);
        }

        private void AddHostButton(Control root, string text, int left)
        {
            Button button = new Button();
            button.Text = text;
            button.Left = left;
            button.Top = 491;
            button.Width = text.Length > 6 ? 64 : 54;
            button.Height = 28;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = border;
            button.BackColor = Color.White;
            button.Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            button.Click += delegate { hostTextBox.Text = text; };
            root.Controls.Add(button);
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

            currentStepLabel.Text = "Aguardando inicio do suporte";
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
            line.Top = 604;
            line.Width = 1024;
            line.Height = 1;
            line.BackColor = border;
            root.Controls.Add(line);

            statusLabel.Text = "Pronto para executar";
            statusLabel.Left = 62;
            statusLabel.Top = 630;
            statusLabel.Width = 300;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.FromArgb(38, 48, 64);
            statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(statusLabel);

            InfoCircle info = new InfoCircle();
            info.Left = 40;
            info.Top = 628;
            info.Width = 18;
            info.Height = 18;
            info.ForeColor = blue;
            root.Controls.Add(info);

            closeWhenDoneCheckBox.Text = "Fechar automaticamente ao finalizar";
            closeWhenDoneCheckBox.Left = 364;
            closeWhenDoneCheckBox.Top = 624;
            closeWhenDoneCheckBox.Width = 225;
            closeWhenDoneCheckBox.Height = 24;
            closeWhenDoneCheckBox.Checked = false;
            closeWhenDoneCheckBox.ForeColor = Color.FromArgb(38, 48, 64);
            closeWhenDoneCheckBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            root.Controls.Add(closeWhenDoneCheckBox);

            executeButton.Text = "Executar";
            executeButton.Left = 604;
            executeButton.Top = 612;
            executeButton.Width = 150;
            executeButton.Height = 40;
            executeButton.FlatStyle = FlatStyle.Flat;
            executeButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            executeButton.BackColor = Color.FromArgb(0, 104, 210);
            executeButton.ForeColor = Color.White;
            executeButton.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
            executeButton.Click += delegate { StartSupport(); };
            root.Controls.Add(executeButton);

            cancelButton.Text = "Cancelar";
            cancelButton.Left = 772;
            cancelButton.Top = 612;
            cancelButton.Width = 124;
            cancelButton.Height = 40;
            cancelButton.FlatStyle = FlatStyle.Flat;
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelSupport(); };
            root.Controls.Add(cancelButton);

            closeButton.Text = "Fechar";
            closeButton.Left = 912;
            closeButton.Top = 612;
            closeButton.Width = 88;
            closeButton.Height = 40;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = border;
            closeButton.BackColor = Color.FromArgb(244, 246, 249);
            closeButton.Enabled = true;
            closeButton.Click += delegate { Close(); };
            root.Controls.Add(closeButton);
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
            selectedPrintersToRemove.Clear();
            selectedPrinterDriversToRemove.Clear();

            DialogResult answer = MessageBox.Show(
                this,
                "Gostaria de remover alguma impressora ou driver atual antes de instalar?",
                "Remover impressora atual",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            using (PrinterRemovalDialog dialog = new PrinterRemovalDialog())
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    selectedPrintersToRemove.AddRange(dialog.SelectedPrinters);
                    selectedPrinterDriversToRemove.AddRange(dialog.SelectedDrivers);
                }
            }
        }

        private void UpdatePrinterSelectionState()
        {
            bool enabled = printerActionOption != null && printerActionOption.CheckBox.Checked;
            printerSelectButton.Enabled = enabled;

            string text = "Nenhuma impressora selecionada";

            if (selectedPrinter != null)
            {
                text = selectedPrinter.BrandName + " / " + selectedPrinter.ModelName;
            }

            printerSelectionLabel.Text = text;
            toolTip.SetToolTip(printerSelectionLabel, text);
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
            progressBar.Value = 0;
            progressLabel.Text = "0% concluido";
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
                executeButton.Enabled = true;
                SetInputsEnabled(true);

                if (cancelRequested)
                {
                    SetProgress(0);
                    currentStepLabel.Text = "Suporte cancelado";
                    statusLabel.Text = "Cancelado pelo usuario";
                    AppendLog("[AVISO] Suporte cancelado.");
                    return;
                }

                if (e.Error != null)
                {
                    currentStepLabel.Text = "Suporte finalizado com erro";
                    statusLabel.Text = "Erro durante o suporte";
                    AppendLog("[ERRO] " + e.Error.Message);
                    return;
                }

                SetProgress(100);
                currentStepLabel.Text = "Suporte finalizado";
                statusLabel.Text = "Processo concluido";
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
            plan.HostServidor = hostTextBox.Text.Trim();

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

            plan.Downloads.Add(new DownloadItem(RawUrl + "/suporte_teksoftware.ps1", "suporte_teksoftware.ps1", "suporte_teksoftware.ps1"));

            if (plan.ContainsAction("firebird"))
            {
                plan.Downloads.Add(new DownloadItem(BaseUrl + "/Firebird-2.5.9.exe", "Firebird-2.5.9.exe", "Firebird-2.5.9.exe"));
            }

            if (plan.ContainsAction("certificados"))
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
                plan.PrintersToRemove.AddRange(selectedPrintersToRemove);
                plan.PrinterDriversToRemove.AddRange(selectedPrinterDriversToRemove);
                plan.Downloads.Add(new DownloadItem(
                    DriversBaseUrl + "/" + selectedPrinter.AssetFile,
                    Path.GetFileName(selectedPrinter.AssetFile),
                    "Driver " + selectedPrinter.BrandName + " " + selectedPrinter.ModelName));
            }

            return plan;
        }

        private void ExecutePlan(WorkPlan plan, BackgroundWorker bg)
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "TekSoftwareSuporte_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(tempDir);

            AppendLog("[INFO] Pasta temporaria: " + tempDir);

            for (int i = 0; i < plan.Downloads.Count; i++)
            {
                if (cancelRequested) return;

                DownloadItem item = plan.Downloads[i];
                string destination = Path.Combine(tempDir, item.FileName);

                bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Baixando " + item.Name + "...");
                AppendLog("[INFO] Baixando: " + item.Name);
                AppendLog("[URL] " + item.Url);

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

            string actionList = String.Join(",", plan.GetActionIds().ToArray());
            string scriptPath = Path.Combine(tempDir, "suporte_teksoftware.ps1");
            string args = "-NoProfile -ExecutionPolicy Bypass -File " + QuoteArgument(scriptPath) + " -Acoes " + QuoteArgument(actionList) + " -HostServidor " + QuoteArgument(plan.HostServidor);

            if (plan.PrinterDriver != null)
            {
                args += " -ImpressoraMarca " + QuoteArgument(plan.PrinterDriver.BrandName);
                args += " -ImpressoraModelo " + QuoteArgument(plan.PrinterDriver.ModelName);
                args += " -ImpressoraArquivo " + QuoteArgument(Path.GetFileName(plan.PrinterDriver.AssetFile));
                args += " -ImpressoraInstalador " + QuoteArgument(plan.PrinterDriver.InstallerPath);
                args += " -RemoverImpressoras " + QuoteArgument(JoinArgumentList(plan.PrintersToRemove));
                args += " -RemoverDriversImpressora " + QuoteArgument(JoinArgumentList(plan.PrinterDriversToRemove));
            }

            bg.ReportProgress(CalcPercent(completedUnits, totalUnits), "Executando suporte TekSoftware...");
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
            for (int i = 0; i < actionOptions.Count; i++)
            {
                actionOptions[i].CheckBox.Enabled = enabled;
            }

            hostTextBox.Enabled = enabled;
            printerSelectButton.Enabled = enabled && printerActionOption != null && printerActionOption.CheckBox.Checked;
            closeWhenDoneCheckBox.Enabled = enabled;
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

    internal sealed class WorkPlan
    {
        public string HostServidor;
        public PrinterDriver PrinterDriver;
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
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
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
            okButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            okButton.BackColor = Color.FromArgb(0, 104, 210);
            okButton.ForeColor = Color.White;
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
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
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

            Text = "Remover impressora ou driver atual";
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
            title.Text = "Remover impressora atual";
            title.Left = 24;
            title.Top = 18;
            title.Width = 460;
            title.Height = 34;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            Controls.Add(title);

            statusLabel.Text = "Selecione somente o que deseja remover antes da instalacao.";
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
            warning.Text = "Dica: remova primeiro a impressora. Marque o driver apenas quando quiser limpar residuos antes de instalar novamente.";
            warning.Left = 24;
            warning.Top = 436;
            warning.Width = 758;
            warning.Height = 24;
            warning.ForeColor = Color.FromArgb(110, 80, 32);
            Controls.Add(warning);

            okButton.Text = "Continuar";
            okButton.Left = 574;
            okButton.Top = 466;
            okButton.Width = 100;
            okButton.Height = 36;
            okButton.FlatStyle = FlatStyle.Flat;
            okButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            okButton.BackColor = Color.FromArgb(0, 104, 210);
            okButton.ForeColor = Color.White;
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
            Color blue = Color.FromArgb(24, 118, 224);

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
