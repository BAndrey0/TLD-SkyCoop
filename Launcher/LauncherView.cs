using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CoopLauncher;

internal sealed partial class LauncherForm
{
	private static readonly Color Ink = Color.FromArgb(16,24,33), Surface = Color.FromArgb(27,38,50), Muted = Color.FromArgb(151,169,182), Accent = Color.FromArgb(136,212,222);
	private readonly Label installedValue = new Label(), latestValue = new Label(), contentValue = new Label();
	private readonly Label sourcePath = new Label();
	private readonly Panel homeView = new Panel(), settingsView = new Panel(), diagnostics = new Panel();
	private readonly Button syncButton = new Button(), sourceButton = new Button(), existingButton = new Button();
	private void InitializeModernView()
	{
		Text="Sky Co-op · The Long Dark"; ClientSize=new Size(1020,700); MinimumSize=new Size(960,700);
		StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=Ink; ForeColor=Color.FromArgb(235,241,244);
		AutoScaleMode=AutoScaleMode.Dpi; DoubleBuffered=true;
		var sidebar=new Panel{Dock=DockStyle.Left,Width=194,BackColor=Color.FromArgb(11,18,26),Padding=new Padding(24)};
		var brand=TextLabel("SKY\nCO-OP",24,FontStyle.Bold,Color.White);brand.SetBounds(24,30,150,88);sidebar.Controls.Add(brand);
		var edition=TextLabel("THE LONG DARK",8,FontStyle.Bold,Muted);edition.SetBounds(26,126,145,20);sidebar.Controls.Add(edition);
		var overview=Nav("Обзор",186);overview.Click+=delegate{ShowSection(false);};
		var settings=Nav("Установка и DLC",232);settings.Click+=delegate{ShowSection(true);};
		folderButton.Text="Открыть папку";FormatButton(folderButton,false);folderButton.SetBounds(16,278,162,38);sidebar.Controls.Add(folderButton);
		var github=Nav("GitHub  ↗",324);github.Click+=delegate{Process.Start("https://github.com/"+Repository+"/releases");};
		var footer=TextLabel("Лаунчер "+LauncherVersion.ToString(3)+"\nНеофициальный кооператив",8.5f,FontStyle.Regular,Muted);footer.Dock=DockStyle.Bottom;footer.Height=50;sidebar.Controls.Add(footer);
		Controls.Add(sidebar);
		var area=new Panel{Dock=DockStyle.Fill,Padding=new Padding(30,24,30,18)};Controls.Add(area);area.BringToFront();
		var heading=new Panel{Dock=DockStyle.Top,Height=56};heading.Controls.Add(TextLabel("Ваше путешествие",20,FontStyle.Bold,ForeColor));
		checkButton.Text="Проверить обновления";FormatButton(checkButton,false);checkButton.Size=new Size(214,36);checkButton.Dock=DockStyle.Right;heading.Controls.Add(checkButton);area.Controls.Add(heading);
		var bottom=new Panel{Dock=DockStyle.Bottom,Height=36};var details=new Button{Text="Диагностика",Dock=DockStyle.Left,Width=135};FormatButton(details,false);details.Click+=delegate{diagnostics.Visible=!diagnostics.Visible;};bottom.Controls.Add(details);
		var channel=TextLabel("Steam · Survival",9,FontStyle.Regular,Muted);channel.Dock=DockStyle.Right;channel.Width=210;channel.TextAlign=ContentAlignment.MiddleRight;bottom.Controls.Add(channel);area.Controls.Add(bottom);
		diagnostics.Dock=DockStyle.Bottom;diagnostics.Height=160;diagnostics.Visible=false;diagnostics.Padding=new Padding(0,8,0,8);
		log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BackColor=Color.FromArgb(10,17,24);log.ForeColor=Muted;log.BorderStyle=BorderStyle.None;log.Font=new Font("Consolas",9);log.Dock=DockStyle.Fill;diagnostics.Controls.Add(log);area.Controls.Add(diagnostics);
		homeView.Dock=DockStyle.Fill;settingsView.Dock=DockStyle.Fill;settingsView.Visible=false;area.Controls.Add(homeView);area.Controls.Add(settingsView);homeView.BringToFront();
		var hero=new LandscapePanel{Dock=DockStyle.Top,Height=216};
		var heroTitle=TextLabel("Выживать.\nВместе.",32,FontStyle.Bold,Color.White);heroTitle.BackColor=Color.Transparent;heroTitle.SetBounds(24,26,360,104);hero.Controls.Add(heroTitle);
		var subtitle=TextLabel("Кооперативное выживание на Великом Медведе",10,FontStyle.Regular,Color.FromArgb(188,210,220));subtitle.BackColor=Color.Transparent;subtitle.SetBounds(27,164,430,30);hero.Controls.Add(subtitle);homeView.Controls.Add(hero);
		var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,22,0,0)};homeView.Controls.Add(body);body.BringToFront();
		var versions=new TableLayoutPanel{Dock=DockStyle.Top,Height=84,ColumnCount=3,BackColor=Surface};for(int i=0;i<3;i++)versions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333f));
		VersionCard(versions,0,"УСТАНОВЛЕНО",installedValue);VersionCard(versions,1,"ДОСТУПНО",latestValue);VersionCard(versions,2,"ДОПОЛНЕНИЯ",contentValue);body.Controls.Add(versions);
		var actions=new Panel{Dock=DockStyle.Bottom,Height=82,Padding=new Padding(0,12,0,10)};
		playButton.Text="Играть  →";FormatButton(playButton,true);playButton.Dock=DockStyle.Left;playButton.Width=245;playButton.Font=new Font("Segoe UI Semibold",15);
		installButton.Text="Установить";FormatButton(installButton,false);installButton.Dock=DockStyle.Right;installButton.Width=226;actions.Controls.Add(playButton);actions.Controls.Add(installButton);body.Controls.Add(actions);
		var statePanel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(0,18,0,0)};body.Controls.Add(statePanel);statePanel.BringToFront();
		statusLabel.Font=new Font("Segoe UI Semibold",13);statusLabel.Dock=DockStyle.Top;statusLabel.Height=68;
		pathLabel.Dock=DockStyle.Top;pathLabel.Height=42;pathLabel.ForeColor=Muted;pathLabel.AutoEllipsis=true;statePanel.Controls.Add(pathLabel);statePanel.Controls.Add(statusLabel);
		progress.Dock=DockStyle.Bottom;progress.Height=5;statePanel.Controls.Add(progress);
		versionLabel.Visible=false;
		BuildSettings();

		Button Nav(string text,int y){var button=new Button{Text=text,TextAlign=ContentAlignment.MiddleLeft};FormatButton(button,false);button.SetBounds(16,y,162,38);sidebar.Controls.Add(button);return button;}
	}
	private static Label TextLabel(string text,float size,FontStyle style,Color color) => new Label{Text=text,Font=new Font("Segoe UI",size,style),ForeColor=color,AutoSize=false,Size=new Size(430,40)};
	private static void FormatButton(Button button,bool primary)
	{
		button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=primary?0:1;button.FlatAppearance.BorderColor=Color.FromArgb(59,78,94);
		button.BackColor=primary?Accent:Surface;button.ForeColor=primary?Ink:Color.FromArgb(226,236,241);button.Cursor=Cursors.Hand;button.TabStop=true;
		button.FlatAppearance.MouseOverBackColor=primary?Color.FromArgb(166,229,235):Color.FromArgb(40,57,72);
	}
	private static void VersionCard(TableLayoutPanel table,int column,string title,Label value)
	{
		var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18,12,8,8)};var label=TextLabel(title,8,FontStyle.Bold,Muted);label.Dock=DockStyle.Top;label.Height=23;
		value.Dock=DockStyle.Fill;value.Font=new Font("Segoe UI Semibold",16);value.AutoEllipsis=true;panel.Controls.Add(value);panel.Controls.Add(label);table.Controls.Add(panel,column,0);
	}
	private void BuildSettings()
	{
		var title=TextLabel("Игра и дополнения",19,FontStyle.Bold,ForeColor);title.Dock=DockStyle.Top;title.Height=60;settingsView.Controls.Add(title);
		var content=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,20,0,0),AutoScroll=true};settingsView.Controls.Add(content);content.BringToFront();
		content.Controls.Add(TextLabel("Оригинальная игра Steam",11,FontStyle.Bold,ForeColor));sourcePath.Size=new Size(650,48);sourcePath.ForeColor=Muted;sourcePath.AutoEllipsis=true;content.Controls.Add(sourcePath);
		sourceButton.Text="Выбрать папку Steam";sourceButton.Size=new Size(240,38);FormatButton(sourceButton,false);sourceButton.Click+=delegate{var path=AskFolder("Оригинальная игра Steam", "tld.exe");if(path!=null){state.gameDirectory=path;SaveState();RefreshView();}};content.Controls.Add(sourceButton);
		var spacer=new Panel{Height=22,Width=10};content.Controls.Add(spacer);
		syncButton.Text="Проверить и добавить DLC";syncButton.Size=new Size(280,40);FormatButton(syncButton,false);syncButton.Click+=async delegate{await SyncContentFromSettings();};content.Controls.Add(syncButton);
		content.Controls.Add(new Label{Text="После загрузки DLC в Steam лаунчер добавит его в кооперативную копию. Повторная полная установка не нужна.",ForeColor=Muted,Size=new Size(650,58),Padding=new Padding(0,10,0,0)});
		existingButton.Text="Подключить готовую установку";existingButton.Size=new Size(280,38);FormatButton(existingButton,false);existingButton.Click+=delegate{var path=AskFolder("Готовая папка кооператива (player)","game\\tld.exe");if(path!=null&&IsInstalled(path)){state.installDirectory=path;SaveState();RefreshView();}};content.Controls.Add(existingButton);
		testBuilds.Text="Получать тестовые версии мода";testBuilds.Size=new Size(420,50);testBuilds.ForeColor=ForeColor;content.Controls.Add(testBuilds);
		content.Controls.Add(new Label{Text="У всех участников должна быть одна версия мода и необходимые для мира DLC.",ForeColor=Muted,Size=new Size(650,45)});
	}
	private void ShowSection(bool settings){settingsView.Visible=settings;homeView.Visible=!settings;if(settings)settingsView.BringToFront();else homeView.BringToFront();}
	private void RefreshDashboard(bool installed,Version current,bool update)
	{
		installedValue.Text=current?.ToString(3)??"Не установлено";latestValue.Text=latest?.Version.ToString(3)??"—";
		contentValue.Text=installed&&File.Exists(Path.Combine(state.installDirectory,"game","tld_dlc","TFTFT","DLC01.json"))?"TFTFT":"Базовая игра";
		sourcePath.Text=string.IsNullOrEmpty(state.gameDirectory)?"Игра будет найдена автоматически при установке":state.gameDirectory;
		syncButton.Enabled=!busy&&installed;sourceButton.Enabled=!busy;existingButton.Enabled=!busy;folderButton.Enabled=!busy;
		playButton.Text="Играть  →";
		installButton.Visible=!installed||update;
		progress.Visible=busy;
	}
	private async System.Threading.Tasks.Task SyncContentFromSettings()
	{
		if(busy||GameRunning())return;SetBusy(true,"Проверяю дополнения Steam…");
		try{await SyncInstalledContent();Log("Дополнения проверены.");}
		catch(Exception ex){Log(ex.Message);MessageBox.Show(this,ex.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Error);}
		finally{SetBusy(false,null);}
	}
	private async Task SyncInstalledContent(bool useInstalledOnMismatch = false)
	{
		string source=state.gameDirectory;
		if(string.IsNullOrEmpty(source)||!File.Exists(Path.Combine(source,"GameAssembly.dll")))source=FindSteamGame();
		if(source==null){Log("Steam-оригинал не найден: DLC не синхронизированы. Укажите путь в «Установка и DLC».");return;}
		state.gameDirectory=source;SaveState();
		if (useInstalledOnMismatch)
		{
			string installedAssembly = Path.Combine(state.installDirectory, "game", "GameAssembly.dll");
			bool compatible = await Task.Run(() =>
			{
				using var sha = SHA256.Create();
				using var original = File.OpenRead(Path.Combine(source, "GameAssembly.dll"));
				using var installed = File.OpenRead(installedAssembly);
				return sha.ComputeHash(original).SequenceEqual(sha.ComputeHash(installed));
			});
			if (!compatible)
			{
				Log("Версии Steam и кооперативной копии различаются. Запускаю установленную копию с её дополнениями.");
				return;
			}
		}
		string script=Path.Combine(state.installDirectory,"Sync-GameContent.ps1");
		if(!File.Exists(script))script=Path.Combine(KitDir,"Sync-GameContent.ps1");
		if(File.Exists(script))await RunScript(script,"-GameDirectory \""+source+"\" -InstallDirectory \""+state.installDirectory+"\"");
	}
}

internal sealed class LandscapePanel : Panel
{
	public LandscapePanel(){DoubleBuffered=true;}
	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
		using(var sky=new LinearGradientBrush(ClientRectangle,Color.FromArgb(39,76,94),Color.FromArgb(17,36,49),90))g.FillRectangle(sky,ClientRectangle);
		float w=Width,h=Height;
		using(var moon=new SolidBrush(Color.FromArgb(188,218,222)))g.FillEllipse(moon,w-113,28,35,35);
		using(var back=new SolidBrush(Color.FromArgb(61,95,111)))g.FillPolygon(back,new[]{new PointF(w*.43f,h),new PointF(w*.7f,40),new PointF(w*.83f,140),new PointF(w*.91f,72),new PointF(w,h*.7f),new PointF(w,h)});
		using(var snow=new SolidBrush(Color.FromArgb(144,179,187)))g.FillPolygon(snow,new[]{new PointF(w*.7f,40),new PointF(w*.64f,88),new PointF(w*.7f,77),new PointF(w*.72f,90),new PointF(w*.74f,72)});
		using(var front=new SolidBrush(Color.FromArgb(21,47,61)))g.FillPolygon(front,new[]{new PointF(w*.45f,h),new PointF(w*.76f,112),new PointF(w*.85f,165),new PointF(w,116),new PointF(w,h)});
		using(var tree=new SolidBrush(Color.FromArgb(13,32,43)))for(int i=0;i<7;i++){float x=w*.6f+i*42,y=h-12-i%3*8;g.FillPolygon(tree,new[]{new PointF(x,y-37-i%2*13),new PointF(x-13,y),new PointF(x+13,y)});}
	}
}
