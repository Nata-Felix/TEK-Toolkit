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
        private const string Repo = "Nata-Felix/TEK-Toolkit";
        private const string BaseUrl = "https://github.com/" + Repo + "/releases/download/" + Version;
        private const string DriversBaseUrl = "https://github.com/" + Repo + "/releases/download/" + DriversVersion;
        private const string DriversIndexUrl = DriversBaseUrl + "/drivers-impressoras.json";
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repo + "/main";
        private const string UrlVersaoNormal = "https://files.tekfarma.com.br/versao/TekFarma50.exe";
        private const string UrlVersaoI = "https://files.tekfarma.com.br/versao/TekFarma50i.exe";
        private const string RadminVpnUrl = "https://download.radmin-vpn.com/download/files/Radmin_VPN_2.0.4899.9.exe";

        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
        private readonly Color textMuted = Color.FromArgb(110, 119, 135);

        private readonly List<ActionOption> actionOptions = new List<ActionOption>();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Label progressLabel = new Label();
        private readonly Label currentStepLabel = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly Button executeButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button closeButton = new Button();
        private readonly CheckBox closeWhenDoneCheckBox = new CheckBox();
        private readonly Label statusLabel = new Label();
        private readonly PictureBox logoBox = new PictureBox();
        private readonly ToolTip toolTip = new ToolTip();
        private readonly Panel rootPanel = new Panel();
        private readonly Panel headerPanel = new Panel();
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
        private PrinterDriver selectedPrinter;
        private ActionOption printerActionOption;
        private string selectedMappingHost = "SERVIDOR";
        private ActionOption mappingActionOption;
        private ServerMigrationPlan selectedServerMigration;
        private ActionOption serverMigrationActionOption;
        private SefazTimeZoneOption selectedSefazTimeZone;
        private ActionOption sefazTlsActionOption;
        private readonly List<string> selectedPrintersToRemove = new List<string>();
        private readonly List<string> selectedPrinterDriversToRemove = new List<string>();

        public SupportForm()
        {
            Text = "Suporte TekSoftware";
            ClientSize = new Size(1040, 700);
            MinimumSize = new Size(640, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            FormClosing += SupportForm_FormClosing;
            Resize += delegate { ApplyResponsiveLayout(); };

            Icon icon = LoadEmbeddedIcon("TekFarmaIcon");
            if (icon != null)
            {
                Icon = icon;
            }

            BuildLayout();
            ApplyResponsiveLayout();
            toolTip.AutoPopDelay = 12000;
            toolTip.InitialDelay = 350;
            toolTip.ReshowDelay = 100;
        }

        private void BuildLayout()
        {
            rootPanel.Dock = DockStyle.Fill;
            rootPanel.BackColor = Color.White;
            rootPanel.AutoScroll = true;
            Controls.Add(rootPanel);

            BuildHeader(rootPanel);
            BuildContent(rootPanel);
            BuildFooter(rootPanel);
        }

        private void BuildHeader(Control root)
        {
            headerPanel.Left = 24;
            headerPanel.Top = 8;
            headerPanel.Width = 992;
            headerPanel.Height = 112;
            headerPanel.BackColor = Color.White;
            root.Controls.Add(headerPanel);

            Image logo = LoadEmbeddedImage("TekFarmaLogo");
            if (logo != null)
            {
                logoBox.Image = logo;
                logoBox.SizeMode = PictureBoxSizeMode.Zoom;
            }

            logoBox.Left = 20;
            logoBox.Top = 8;
            logoBox.Width = 220;
            logoBox.Height = 88;
            headerPanel.Controls.Add(logoBox);

            Panel divider = new Panel();
            divider.Left = 270;
            divider.Top = 14;
            divider.Width = 1;
            divider.Height = 76;
            divider.BackColor = border;
            headerPanel.Controls.Add(divider);

            Label title = new Label();
            title.Text = "Suporte TekSoftware";
            title.Left = 300;
            title.Top = 20;
            title.Width = 650;
            title.Height = 38;
            title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            title.Font = new Font("Segoe UI", 19F, FontStyle.Bold, GraphicsUnit.Point);
            title.ForeColor = darkBlue;
            headerPanel.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "Ferramentas de suporte e manutencao";
            subtitle.Left = 304;
            subtitle.Top = 62;
            subtitle.Width = 646;
            subtitle.Height = 28;
            subtitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            subtitle.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            subtitle.ForeColor = textMuted;
            headerPanel.Controls.Add(subtitle);

            Panel horizontal = new Panel();
            horizontal.Left = 0;
            horizontal.Top = 104;
            horizontal.Width = 992;
            horizontal.Height = 1;
            horizontal.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            horizontal.BackColor = border;
            headerPanel.Controls.Add(horizontal);
        }

        private void BuildContent(Control root)
        {
            selectLabel.Text = "Selecione as acoes de suporte";
            selectLabel.Left = 26;
            selectLabel.Top = 130;
            selectLabel.Width = 440;
            selectLabel.Height = 24;
            selectLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            selectLabel.ForeColor = blue;
            root.Controls.Add(selectLabel);

            actionsPanel.Left = 24;
            actionsPanel.Top = 158;
            actionsPanel.Width = 440;
            actionsPanel.Height = 460;
            actionsPanel.AutoScroll = true;
            actionsPanel.FlowDirection = FlowDirection.TopDown;
            actionsPanel.WrapContents = false;
            actionsPanel.Padding = new Padding(4);
            actionsPanel.BackColor = Color.White;
            actionsPanel.BorderStyle = BorderStyle.FixedSingle;
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
            AddSection(actionsPanel, "Rede e acesso", SectionIconKind.Network, ref y);
            AddAction(actionsPanel, "rede", "Configurar rede avancada", "Ativa servicos, firewall de rede, bindings e parametros de compartilhamento.", ref y);
            AddAction(actionsPanel, "credencial", "Criar credencial SERVIDOR", "Cria credencial SERVIDOR com usuario convidado e senha vazia.", ref y);
            mappingActionOption = AddAction(actionsPanel, "mapear", "Mapear TekSoftware", "Remove mapeamentos TekSoftware antigos, usa host informado e escolhe Z:, Y:, X:...", ref y);
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
            AddAction(actionsPanel, "firewall", "Adicionar excecao no firewall", "Executa os BATs e cria regras para executaveis TekSoftware encontrados.", ref y);
            AddAction(actionsPanel, "farmaciapopular", "Instalar Farmacia Popular GBAS", "Baixa o GBAS, copia para TekFarma, abre a identificacao do terminal e o portal.", ref y);

            AddSection(actionsPanel, "Softwares", SectionIconKind.Software, ref y);
            AddAction(actionsPanel, "radminvpn", "Instalar Radmin VPN", "Baixa e instala Radmin VPN em modo silencioso, depois abre a interface para criar a rede.", ref y);

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

        private void AddSection(Panel parent, string text, SectionIconKind iconKind, ref int y)
        {
            CollapsibleSection section = new CollapsibleSection(text, iconKind, blue, border);
            section.Width = Math.Max(280, actionsPanel.ClientSize.Width - 16);
            section.ExpandedChanged += delegate { actionsPanel.PerformLayout(); };
            actionsPanel.Controls.Add(section);
            actionSections.Add(section);
            activeActionSection = section;
            y = 0;
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
            checkBox.ForeColor = Color.FromArgb(28, 36, 48);
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
            card.BorderStyle = BorderStyle.FixedSingle;
            root.Controls.Add(card);
            return card;
        }

        private void BuildProgressPanel(Control root)
        {
            Panel card = ConfigureCard(progressCard, root, 482, 158, 534, 312);

            Label progressTitle = new Label();
            progressTitle.Text = "Progresso da execucao";
            progressTitle.Left = 18;
            progressTitle.Top = 14;
            progressTitle.Width = 360;
            progressTitle.Height = 24;
            progressTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            progressTitle.ForeColor = blue;
            card.Controls.Add(progressTitle);

            progressBar.Left = 18;
            progressBar.Top = 52;
            progressBar.Width = 410;
            progressBar.Height = 28;
            progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            card.Controls.Add(progressBar);

            progressLabel.Text = "0% concluido";
            progressLabel.Left = 446;
            progressLabel.Top = 55;
            progressLabel.Width = 90;
            progressLabel.Height = 24;
            progressLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            progressLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            progressLabel.ForeColor = blue;
            card.Controls.Add(progressLabel);

            GearPanel gear = new GearPanel();
            gear.Left = 18;
            gear.Top = 96;
            gear.Width = 34;
            gear.Height = 34;
            gear.ForeColor = Color.FromArgb(24, 118, 224);
            card.Controls.Add(gear);

            currentStepLabel.Text = "Aguardando inicio do suporte";
            currentStepLabel.Left = 60;
            currentStepLabel.Top = 100;
            currentStepLabel.Width = 470;
            currentStepLabel.Height = 28;
            currentStepLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            currentStepLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            currentStepLabel.ForeColor = Color.FromArgb(35, 43, 55);
            card.Controls.Add(currentStepLabel);

            Panel separator = new Panel();
            separator.Left = 18;
            separator.Top = 144;
            separator.Width = 518;
            separator.Height = 1;
            separator.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            separator.BackColor = border;
            card.Controls.Add(separator);

            Label logTitle = new Label();
            logTitle.Text = "Log de execucao (PowerShell)";
            logTitle.Left = 18;
            logTitle.Top = 154;
            logTitle.Width = 360;
            logTitle.Height = 24;
            logTitle.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            logTitle.ForeColor = blue;
            card.Controls.Add(logTitle);

            logBox.Left = 18;
            logBox.Top = 182;
            logBox.Width = 518;
            logBox.Height = 108;
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Font = new Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            logBox.BackColor = Color.White;
            logBox.ForeColor = Color.FromArgb(24, 30, 38);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            card.Controls.Add(logBox);
        }

        private void BuildFooter(Control root)
        {
            Panel line = new Panel();
            line.Left = 0;
            line.Top = 0;
            line.Width = 1040;
            line.Height = 1;
            line.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            line.BackColor = border;
            footerPanel.Controls.Add(line);

            footerPanel.Left = 0;
            footerPanel.Top = 638;
            footerPanel.Width = 1040;
            footerPanel.Height = 62;
            footerPanel.BackColor = Color.White;
            root.Controls.Add(footerPanel);

            statusLabel.Text = "Pronto para executar";
            statusLabel.Left = 62;
            statusLabel.Top = 22;
            statusLabel.Width = 300;
            statusLabel.Height = 24;
            statusLabel.ForeColor = Color.FromArgb(38, 48, 64);
            statusLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            footerPanel.Controls.Add(statusLabel);

            InfoCircle info = new InfoCircle();
            info.Left = 40;
            info.Top = 20;
            info.Width = 18;
            info.Height = 18;
            info.ForeColor = blue;
            footerPanel.Controls.Add(info);

            closeWhenDoneCheckBox.Text = "Fechar ao concluir";
            closeWhenDoneCheckBox.Left = 420;
            closeWhenDoneCheckBox.Top = 18;
            closeWhenDoneCheckBox.Width = 160;
            closeWhenDoneCheckBox.Height = 24;
            closeWhenDoneCheckBox.Checked = false;
            closeWhenDoneCheckBox.ForeColor = Color.FromArgb(38, 48, 64);
            closeWhenDoneCheckBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            footerPanel.Controls.Add(closeWhenDoneCheckBox);

            executeButton.Text = "Executar";
            executeButton.Left = 720;
            executeButton.Top = 10;
            executeButton.Width = 130;
            executeButton.Height = 40;
            executeButton.FlatStyle = FlatStyle.Flat;
            executeButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            executeButton.BackColor = Color.FromArgb(0, 104, 210);
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
            cancelButton.FlatAppearance.BorderColor = border;
            cancelButton.BackColor = Color.White;
            cancelButton.Enabled = false;
            cancelButton.Click += delegate { CancelSupport(); };
            footerPanel.Controls.Add(cancelButton);

            closeButton.Text = "Fechar";
            closeButton.Left = 1028;
            closeButton.Top = 10;
            closeButton.Width = 88;
            closeButton.Height = 40;
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderColor = border;
            closeButton.BackColor = Color.FromArgb(244, 246, 249);
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

            headerPanel.Left = margin;
            headerPanel.Top = 8;
            headerPanel.Width = viewportWidth - (margin * 2);
            headerPanel.Height = 112;

            selectLabel.Left = margin + 2;
            selectLabel.Top = 130;
            selectLabel.Width = viewportWidth - (margin * 2);

            int contentTop = 158;
            int footerTop;
            int footerHeight;

            if (!compact)
            {
                footerHeight = 62;
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
                footerHeight = 100;
                int contentWidth = viewportWidth - (margin * 2);
                int y = contentTop;

                actionsPanel.SetBounds(margin, y, contentWidth, 300);
                y += actionsPanel.Height + gap;
                progressCard.SetBounds(margin, y, contentWidth, 300);
                y += progressCard.Height + gap;
                footerTop = y;
            }

            LayoutFooter(viewportWidth, footerTop, footerHeight, compact);
            rootPanel.AutoScrollMinSize = new Size(0, footerTop + footerHeight);
            ResizeActionSections();
        }

        private void LayoutFooter(int width, int top, int height, bool compact)
        {
            footerPanel.SetBounds(0, top, width, height);

            if (compact)
            {
                statusLabel.SetBounds(62, 16, 240, 24);
                closeWhenDoneCheckBox.SetBounds(Math.Max(320, width - 188), 14, 160, 24);

                closeButton.SetBounds(width - 112, 50, 88, 40);
                cancelButton.SetBounds(closeButton.Left - 120, 50, 108, 40);
                executeButton.SetBounds(cancelButton.Left - 142, 50, 130, 40);
            }
            else
            {
                statusLabel.SetBounds(62, 22, 250, 24);
                closeButton.SetBounds(width - 112, 10, 88, 40);
                cancelButton.SetBounds(closeButton.Left - 120, 10, 108, 40);
                executeButton.SetBounds(cancelButton.Left - 142, 10, 130, 40);
                closeWhenDoneCheckBox.SetBounds(Math.Max(320, executeButton.Left - 180), 18, 160, 24);
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

            plan.Downloads.Add(new DownloadItem(RawUrl + "/suporte_teksoftware.ps1", "suporte_teksoftware.ps1", "suporte_teksoftware.ps1"));

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
                            plan.Downloads.Add(new DownloadItem(UrlVersaoI, "TekFarma50i.exe", "TekFarma50i.exe"));
                        }
                        else
                        {
                            plan.Downloads.Add(new DownloadItem(UrlVersaoNormal, "TekFarma50.exe", "TekFarma50.exe"));
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

        private void ExecutePlan(WorkPlan plan, BackgroundWorker bg)
        {
            tempDir = Path.Combine(
                Path.GetTempPath(),
                "TekSoftwareSuporte_" + Process.GetCurrentProcess().Id + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(tempDir);
            guiLogPath = Path.Combine(tempDir, "TekSoftwareSuporte_GUI.log");

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

    internal sealed class CollapsibleSection : Panel
    {
        private const int HeaderHeight = 36;
        private readonly Panel header = new Panel();
        private readonly Panel content = new Panel();
        private readonly Label titleLabel = new Label();
        private readonly Label indicatorLabel = new Label();
        private int optionCount;
        private bool expanded;

        public event EventHandler ExpandedChanged;

        public CollapsibleSection(string title, SectionIconKind iconKind, Color accent, Color borderColor)
        {
            Height = HeaderHeight;
            Margin = new Padding(4, 3, 4, 1);
            BackColor = Color.White;
            BorderStyle = BorderStyle.FixedSingle;

            header.Dock = DockStyle.Top;
            header.Height = HeaderHeight;
            header.BackColor = Color.FromArgb(246, 249, 253);
            header.Cursor = Cursors.Hand;
            Controls.Add(header);

            SectionIcon icon = new SectionIcon(iconKind);
            icon.Left = 12;
            icon.Top = 8;
            icon.Width = 18;
            icon.Height = 18;
            icon.ForeColor = accent;
            icon.Cursor = Cursors.Hand;
            header.Controls.Add(icon);

            titleLabel.Text = title;
            titleLabel.Left = 40;
            titleLabel.Top = 7;
            titleLabel.Width = 250;
            titleLabel.Height = 22;
            titleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.ForeColor = accent;
            titleLabel.Cursor = Cursors.Hand;
            titleLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.Controls.Add(titleLabel);

            indicatorLabel.Text = ">";
            indicatorLabel.TextAlign = ContentAlignment.MiddleCenter;
            indicatorLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
            indicatorLabel.ForeColor = accent;
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

        private void ToggleExpanded(object sender, EventArgs e)
        {
            expanded = !expanded;
            content.Visible = expanded;
            indicatorLabel.Text = expanded ? "v" : ">";
            Height = HeaderHeight + (expanded ? content.Height : 0);

            if (Parent != null)
            {
                Parent.PerformLayout();
            }

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

    internal sealed class WorkPlan
    {
        public string HostServidor;
        public PrinterDriver PrinterDriver;
        public ServerMigrationPlan ServerMigration;
        public SefazTimeZoneOption SefazTimeZone;
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

    internal sealed class MappingHostDialog : Form
    {
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
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
            title.Text = "Mapear TekSoftware";
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
            AddPresetButton("SERVERTEK", 400);

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
            okButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            okButton.BackColor = Color.FromArgb(0, 104, 210);
            okButton.ForeColor = Color.White;
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
                MessageBox.Show(this, "Informe o host do servidor.", "Mapear TekSoftware", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

    internal sealed class SefazTimeZoneDialog : Form
    {
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
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
            okButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            okButton.BackColor = Color.FromArgb(0, 104, 210);
            okButton.ForeColor = Color.White;
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
        private readonly Color blue = Color.FromArgb(0, 92, 190);
        private readonly Color darkBlue = Color.FromArgb(0, 49, 112);
        private readonly Color border = Color.FromArgb(205, 214, 224);
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
            versionLabel.Text = "Versao TekFarma";
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

            copiarPrincipalCheckBox.Text = "Pre-copiar TekSoftware sem pastas finais/pesadas";
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

            configurarRedeCheckBox.Text = "Configurar rede e compartilhar C:\\TekSoftware";
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
            okButton.FlatAppearance.BorderColor = Color.FromArgb(0, 76, 170);
            okButton.BackColor = Color.FromArgb(0, 104, 210);
            okButton.ForeColor = Color.White;
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

    internal enum SectionIconKind
    {
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
