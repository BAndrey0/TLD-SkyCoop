using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CoopLauncher;

// TLD-SkyCoop launcher: installs the separate co-op copy of the game, keeps the mod updated from GitHub releases and
// starts the game. It drives the same kit scripts as Install.cmd / Update.cmd / Play-Coop.cmd (downloaded with each
// release into friend-kit next to the launcher), so installing by hand and through the launcher give the same result.
//
// Folder layout (the launcher's own folder is the root):
//   TLD-Coop-Launcher.exe, launcher.json
//   friend-kit\   kit of the installed or newest release
//   downloads\    release archives, MelonLoader, .NET runtime, SkyCoop assets (cached, checksummed by the scripts)
//   player\       the co-op copy of the game (default; any English-letter path can be chosen)
internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		if (args.Length > 0 && args[0] == "--check")
		{
			File.WriteAllText(args.Length > 1 ? args[1] : "launcher-check.txt", LauncherForm.SelfCheck(), Encoding.UTF8);
			return;
		}
		// The copy inside friend-kit moves itself next to friend-kit first: updating the kit replaces that folder.
		string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
		if (string.Equals(Path.GetFileName(dir), "friend-kit", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir, "manifest.json")))
		{
			try
			{
				string outside = Path.Combine(Path.GetDirectoryName(dir), "TLD-Coop-Launcher.exe");
				File.Copy(Application.ExecutablePath, outside, overwrite: true);
				Process.Start(outside);
				return;
			}
			catch
			{
			}
		}
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run(new LauncherForm());
	}
}

internal sealed class LauncherState
{
	public string installDirectory = "";

	public string gameDirectory = "";

	public bool includeTestBuilds = true;
}

internal sealed class ReleaseInfo
{
	public string Tag = "";

	public string Name = "";

	public Version Version;

	public bool Prerelease;

	public string ZipUrl = "";

	public string ZipName = "";

	public string LauncherUrl = "";
}

internal sealed class LauncherForm : Form
{
	private const string Repository = "BAndrey0/TLD-SkyCoop";

	private static readonly Version LauncherVersion = typeof(LauncherForm).Assembly.GetName().Version;

	private readonly string root = ResolveRoot();

	// Started from inside friend-kit (the copy shipped in the release archive): work from the folder above it, so
	// updating the kit does not delete the running launcher.
	private static string ResolveRoot()
	{
		string dir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
		if (string.Equals(Path.GetFileName(dir), "friend-kit", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(dir, "manifest.json")))
		{
			return Path.GetDirectoryName(dir);
		}
		return dir;
	}

	private readonly Label statusLabel = new Label();

	private readonly Label versionLabel = new Label();

	private readonly Label pathLabel = new Label();

	private readonly Button playButton = new Button();

	private readonly Button installButton = new Button();

	private readonly Button checkButton = new Button();

	private readonly Button folderButton = new Button();

	private readonly CheckBox testBuilds = new CheckBox();

	private readonly ProgressBar progress = new ProgressBar();

	private readonly TextBox log = new TextBox();

	private LauncherState state = new LauncherState();

	private ReleaseInfo latest;

	private bool busy;

	private string StatePath => Path.Combine(root, "launcher.json");

	private string KitDir => Path.Combine(root, "friend-kit");

	private string DownloadsDir => Path.Combine(root, "downloads");

	public LauncherForm()
	{
		Text = "TLD-SkyCoop — лаунчер";
		ClientSize = new Size(640, 470);
		FormBorderStyle = FormBorderStyle.FixedSingle;
		MaximizeBox = false;
		StartPosition = FormStartPosition.CenterScreen;
		Font = new Font("Segoe UI", 9.5f);
		BackColor = Color.FromArgb(24, 26, 28);
		ForeColor = Color.FromArgb(225, 222, 212);

		Label title = new Label { Text = "The Long Dark — кооператив", Font = new Font("Segoe UI Semibold", 16f), AutoSize = true, Location = new Point(18, 14) };
		versionLabel.SetBounds(20, 52, 600, 22);
		pathLabel.SetBounds(20, 74, 600, 22);
		pathLabel.ForeColor = Color.FromArgb(150, 150, 145);
		statusLabel.SetBounds(20, 100, 600, 40);

		StyleButton(playButton, "ИГРАТЬ", 20, 146, 200, 46, primary: true);
		StyleButton(installButton, "Установить", 232, 146, 180, 46, primary: false);
		StyleButton(checkButton, "Проверить обновления", 424, 146, 196, 22, primary: false);
		StyleButton(folderButton, "Открыть папку", 424, 170, 196, 22, primary: false);
		testBuilds.Text = "Получать тестовые версии";
		testBuilds.SetBounds(20, 200, 300, 24);
		progress.SetBounds(20, 228, 600, 14);
		log.SetBounds(20, 250, 600, 206);
		log.Multiline = true;
		log.ReadOnly = true;
		log.ScrollBars = ScrollBars.Vertical;
		log.BackColor = Color.FromArgb(14, 15, 16);
		log.ForeColor = Color.FromArgb(190, 188, 180);
		log.BorderStyle = BorderStyle.FixedSingle;
		log.Font = new Font("Consolas", 8.5f);

		Controls.AddRange(new Control[] { title, versionLabel, pathLabel, statusLabel, playButton, installButton, checkButton, folderButton, testBuilds, progress, log });

		playButton.Click += async delegate { await Play(); };
		installButton.Click += async delegate { await InstallOrUpdate(); };
		checkButton.Click += async delegate { await CheckForUpdates(); };
		folderButton.Click += delegate { Process.Start("explorer.exe", "\"" + root + "\""); };
		testBuilds.CheckedChanged += async delegate
		{
			state.includeTestBuilds = testBuilds.Checked;
			SaveState();
			if (!busy)
			{
				await CheckForUpdates();
			}
		};
		Shown += async delegate { await Startup(); };
	}

	private static void StyleButton(Button button, string text, int x, int y, int w, int h, bool primary)
	{
		button.Text = text;
		button.SetBounds(x, y, w, h);
		button.FlatStyle = FlatStyle.Flat;
		button.FlatAppearance.BorderColor = Color.FromArgb(90, 88, 80);
		button.BackColor = primary ? Color.FromArgb(150, 40, 32) : Color.FromArgb(44, 46, 48);
		button.ForeColor = Color.FromArgb(235, 232, 222);
		if (primary)
		{
			button.Font = new Font("Segoe UI Semibold", 12f);
		}
	}

	// ---------------------------------------------------------------- state

	private async Task Startup()
	{
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		LoadState();
		testBuilds.Checked = state.includeTestBuilds;
		Log("Лаунчер " + LauncherVersion.ToString(3) + ", папка: " + root);
		if (Regex.IsMatch(root, "[^\\x00-\\x7F]"))
		{
			Log("ВНИМАНИЕ: в пути к лаунчеру есть русские буквы. MelonLoader с таким путём работает ненадёжно. Перенесите папку лаунчера, например, в D:\\TLD-Coop.");
		}
		RefreshView();
		await CheckForUpdates();
	}

	private void LoadState()
	{
		try
		{
			if (File.Exists(StatePath))
			{
				state = new JavaScriptSerializer().Deserialize<LauncherState>(File.ReadAllText(StatePath)) ?? new LauncherState();
			}
		}
		catch (Exception ex)
		{
			Log("Не удалось прочитать launcher.json: " + ex.Message);
		}
		if (string.IsNullOrEmpty(state.installDirectory) || !IsInstalled(state.installDirectory))
		{
			string found = FindExistingInstall();
			if (found != null)
			{
				state.installDirectory = found;
				SaveState();
			}
		}
	}

	private void SaveState()
	{
		try
		{
			File.WriteAllText(StatePath, new JavaScriptSerializer().Serialize(state), Encoding.UTF8);
		}
		catch (Exception ex)
		{
			Log("Не удалось сохранить launcher.json: " + ex.Message);
		}
	}

	// An installation made earlier with Install.cmd next to the launcher.
	private string FindExistingInstall()
	{
		try
		{
			string pointer = Path.Combine(root, "coop-install-location.json");
			if (File.Exists(pointer))
			{
				Dictionary<string, object> data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(pointer));
				if (data != null && data.TryGetValue("installDirectory", out object dir) && dir is string path && IsInstalled(path))
				{
					return path;
				}
			}
		}
		catch
		{
		}
		string player = Path.Combine(root, "player");
		return IsInstalled(player) ? player : null;
	}

	private static bool IsInstalled(string dir)
	{
		return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "game", "tld.exe")) && File.Exists(Path.Combine(dir, "game", "Mods", "SkyCoop.dll")) && File.Exists(Path.Combine(dir, "Launch-Coop.ps1"));
	}

	private Version InstalledVersion()
	{
		try
		{
			string file = Path.Combine(state.installDirectory, "coop-version.json");
			if (!File.Exists(file))
			{
				file = Path.Combine(KitDir, "manifest.json");
			}
			if (File.Exists(file))
			{
				Dictionary<string, object> data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
				if (data != null && data.TryGetValue("modVersion", out object value) && Version.TryParse(value as string, out Version version))
				{
					return version;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	private void RefreshView()
	{
		bool installed = IsInstalled(state.installDirectory);
		Version current = installed ? InstalledVersion() : null;
		string latestText = (latest != null) ? (latest.Version.ToString(3) + (latest.Prerelease ? " (тестовая)" : "")) : "—";
		versionLabel.Text = "Установлено: " + ((current != null) ? current.ToString(3) : (installed ? "неизвестно" : "нет")) + "    •    Последняя: " + latestText;
		pathLabel.Text = installed ? ("Игра: " + state.installDirectory) : "Кооперативная копия игры ещё не установлена";
		bool updateAvailable = installed && latest != null && (current == null || latest.Version > current);
		playButton.Enabled = !busy && installed;
		installButton.Enabled = !busy && latest != null && (!installed || updateAvailable);
		installButton.Text = !installed ? "Установить" : (updateAvailable ? ("Обновить до " + latest.Version.ToString(3)) : "Обновлений нет");
		checkButton.Enabled = !busy;
		testBuilds.Enabled = !busy;
		if (!busy)
		{
			statusLabel.Text = !installed ? "Нажмите «Установить»: лаунчер найдёт игру в Steam, скопирует её и поставит мод." : (updateAvailable ? "Есть обновление. Все игроки должны быть на одной версии." : "Всё готово. Запустите Steam и нажмите «Играть».");
		}
	}

	private void SetBusy(bool value, string status)
	{
		busy = value;
		progress.Style = value ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
		progress.Value = 0;
		if (status != null)
		{
			statusLabel.Text = status;
		}
		RefreshView();
	}

	private void Log(string text)
	{
		if (InvokeRequired)
		{
			BeginInvoke(new Action<string>(Log), text);
			return;
		}
		log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + text + Environment.NewLine);
	}

	// ---------------------------------------------------------------- GitHub

	private async Task CheckForUpdates()
	{
		if (busy)
		{
			return;
		}
		SetBusy(true, "Проверяю обновления на GitHub…");
		try
		{
			latest = await Task.Run(() => FetchLatest(state.includeTestBuilds));
			Log((latest != null) ? ("Последняя версия на GitHub: " + latest.Tag + (latest.Prerelease ? " (тестовая)" : "")) : "На GitHub не найдено подходящих релизов.");
			if (latest != null && !string.IsNullOrEmpty(latest.LauncherUrl))
			{
				await MaybeUpdateLauncher(latest);
			}
		}
		catch (Exception ex)
		{
			Log("Не удалось проверить обновления: " + ex.Message);
		}
		finally
		{
			SetBusy(false, null);
		}
	}

	private static WebClient Client()
	{
		WebClient client = new WebClient();
		client.Headers[HttpRequestHeader.UserAgent] = "TLD-Coop-Launcher";
		return client;
	}

	private static ReleaseInfo FetchLatest(bool includeTest)
	{
		string json;
		using (WebClient client = Client())
		{
			client.Encoding = Encoding.UTF8;
			json = client.DownloadString("https://api.github.com/repos/" + Repository + "/releases?per_page=30");
		}
		object[] releases = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as object[];
		List<ReleaseInfo> candidates = new List<ReleaseInfo>();
		foreach (object item in releases ?? new object[0])
		{
			if (!(item is Dictionary<string, object> release) || (release.TryGetValue("draft", out object draft) && draft is bool d && d))
			{
				continue;
			}
			bool pre = release.TryGetValue("prerelease", out object p) && p is bool b && b;
			if (pre && !includeTest)
			{
				continue;
			}
			string tag = release.TryGetValue("tag_name", out object t) ? (t as string ?? "") : "";
			if (!Version.TryParse(tag.TrimStart('v', 'V'), out Version version))
			{
				continue;
			}
			ReleaseInfo info = new ReleaseInfo { Tag = tag, Name = release.TryGetValue("name", out object n) ? (n as string ?? tag) : tag, Version = version, Prerelease = pre };
			if (release.TryGetValue("assets", out object assets) && assets is object[] list)
			{
				foreach (object a in list)
				{
					if (!(a is Dictionary<string, object> asset))
					{
						continue;
					}
					string name = asset.TryGetValue("name", out object an) ? (an as string ?? "") : "";
					string url = asset.TryGetValue("browser_download_url", out object au) ? (au as string ?? "") : "";
					if (Regex.IsMatch(name, "^TLD-SkyCoop-.*\\.zip$", RegexOptions.IgnoreCase))
					{
						info.ZipName = name;
						info.ZipUrl = url;
					}
					else if (name.Equals("TLD-Coop-Launcher.exe", StringComparison.OrdinalIgnoreCase))
					{
						info.LauncherUrl = url;
					}
				}
			}
			if (!string.IsNullOrEmpty(info.ZipUrl))
			{
				candidates.Add(info);
			}
		}
		return candidates.OrderByDescending((ReleaseInfo r) => r.Version).FirstOrDefault();
	}

	private async Task<string> Download(string url, string fileName)
	{
		Directory.CreateDirectory(DownloadsDir);
		string target = Path.Combine(DownloadsDir, fileName);
		string partial = target + ".part";
		Log("Скачиваю " + fileName + "…");
		using (WebClient client = Client())
		{
			client.DownloadProgressChanged += delegate(object s, DownloadProgressChangedEventArgs e)
			{
				BeginInvoke((Action)delegate
				{
					if (e.TotalBytesToReceive > 0)
					{
						progress.Style = ProgressBarStyle.Blocks;
						progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage));
					}
				});
			};
			await client.DownloadFileTaskAsync(new Uri(url), partial);
		}
		if (File.Exists(target))
		{
			File.Delete(target);
		}
		File.Move(partial, target);
		progress.Style = ProgressBarStyle.Marquee;
		return target;
	}

	// The launcher replaces itself when a release carries a newer TLD-Coop-Launcher.exe.
	private async Task MaybeUpdateLauncher(ReleaseInfo release)
	{
		try
		{
			string file = await Download(release.LauncherUrl, "TLD-Coop-Launcher.new.exe");
			Version version = AssemblyNameVersion(file);
			if (version == null || version <= LauncherVersion)
			{
				File.Delete(file);
				return;
			}
			Log("Найдена новая версия лаунчера " + version.ToString(3) + ", перезапускаюсь…");
			string self = Application.ExecutablePath;
			string script = Path.Combine(DownloadsDir, "update-launcher.cmd");
			File.WriteAllText(script, "@echo off\r\nping -n 3 127.0.0.1 >nul\r\nmove /Y \"" + file + "\" \"" + self + "\" >nul\r\nstart \"\" \"" + self + "\"\r\n", Encoding.ASCII);
			Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"") { CreateNoWindow = true, UseShellExecute = false });
			Application.Exit();
		}
		catch (Exception ex)
		{
			Log("Обновление лаунчера пропущено: " + ex.Message);
		}
	}

	private static Version AssemblyNameVersion(string file)
	{
		try
		{
			FileVersionInfo info = FileVersionInfo.GetVersionInfo(file);
			return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
		}
		catch
		{
			return null;
		}
	}

	private async Task FetchKit(ReleaseInfo release)
	{
		string zip = await Download(release.ZipUrl, release.ZipName);
		string staging = Path.Combine(DownloadsDir, "kit-" + release.Version.ToString(3));
		if (Directory.Exists(staging))
		{
			Directory.Delete(staging, recursive: true);
		}
		ZipFile.ExtractToDirectory(zip, staging);
		string extracted = Path.Combine(staging, "friend-kit");
		if (!File.Exists(Path.Combine(extracted, "manifest.json")))
		{
			throw new InvalidOperationException("в архиве релиза нет friend-kit\\manifest.json");
		}
		if (Directory.Exists(KitDir))
		{
			Directory.Delete(KitDir, recursive: true);
		}
		Directory.Move(extracted, KitDir);
		Directory.Delete(staging, recursive: true);
		Log("Набор мода " + release.Version.ToString(3) + " распакован в friend-kit.");
	}

	// ---------------------------------------------------------------- install / update / play

	private async Task InstallOrUpdate()
	{
		if (busy || latest == null)
		{
			return;
		}
		bool installed = IsInstalled(state.installDirectory);
		if (installed && GameRunning())
		{
			MessageBox.Show(this, "Закройте игру через меню паузы и повторите.", Text);
			return;
		}
		string gameDir = null;
		string installDir = state.installDirectory;
		if (!installed)
		{
			DialogResult existing = MessageBox.Show(this, "Кооператив уже ставили раньше (есть папка player с папкой game внутри)?\n\n«Да» — указать эту папку, лаунчер её обновит.\n«Нет» — новая установка из игры Steam.", Text, MessageBoxButtons.YesNoCancel);
			if (existing == DialogResult.Cancel)
			{
				return;
			}
			if (existing == DialogResult.Yes)
			{
				string folder = AskFolder("Укажите папку кооперативной копии (обычно player)", "game\\tld.exe");
				if (folder == null)
				{
					return;
				}
				if (!IsInstalled(folder))
				{
					MessageBox.Show(this, "В этой папке нет установленного кооператива (game\\Mods\\SkyCoop.dll и Launch-Coop.ps1).", Text);
					return;
				}
				state.installDirectory = folder;
				SaveState();
				installed = true;
				installDir = folder;
			}
		}
		if (!installed)
		{
			gameDir = FindSteamGame();
			if (gameDir != null && MessageBox.Show(this, "Найдена игра:\n" + gameDir + "\n\nИспользовать её? Оригинал не изменится, мод ставится в отдельную копию (~21 ГБ).", Text, MessageBoxButtons.YesNo) != DialogResult.Yes)
			{
				gameDir = null;
			}
			if (gameDir == null)
			{
				gameDir = AskFolder("Укажите папку с The Long Dark из Steam (где лежит tld.exe)", "tld.exe");
				if (gameDir == null)
				{
					return;
				}
			}
			installDir = Path.Combine(root, "player");
			if (Directory.Exists(Path.Combine(installDir, "game")))
			{
				MessageBox.Show(this, "Папка " + installDir + "\\game уже существует, но установка в ней не завершена. Удалите её или перенесите и повторите.", Text);
				return;
			}
			state.gameDirectory = gameDir;
		}
		SetBusy(true, installed ? "Обновляю мод…" : "Устанавливаю: копирую игру (~21 ГБ) и ставлю мод. Это займёт время.");
		try
		{
			await FetchKit(latest);
			if (installed)
			{
				await RunScript(Path.Combine(KitDir, "Update-Coop.ps1"), "-InstallDirectory \"" + installDir + "\"");
			}
			else
			{
				await RunScript(Path.Combine(KitDir, "Install-Lab.ps1"), "-GameDirectory \"" + gameDir + "\" -InstallDirectory \"" + installDir + "\"");
				await RunScript(Path.Combine(KitDir, "Update-Coop.ps1"), "-InstallDirectory \"" + installDir + "\"");
			}
			state.installDirectory = installDir;
			SaveState();
			Log(installed ? "Обновление установлено." : "Установка завершена.");
		}
		catch (Exception ex)
		{
			Log("ОШИБКА: " + ex.Message);
			string message = ex.Message;
			if (message.IndexOf("does not match", StringComparison.OrdinalIgnoreCase) >= 0 || message.IndexOf("mismatch", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				message = "Версия игры в Steam отличается от той, на которой проверен мод (Survival 2.55, сборка 22915504). Скорее всего, Steam обновил игру.\n\nЕсли кооператив уже установлен (папка player), запустите установку ещё раз и выберите «Да», указав эту папку: отдельная копия игры не обновляется Steam и продолжит работать.\n\n" + ex.Message;
			}
			MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		finally
		{
			SetBusy(false, null);
		}
	}

	private async Task Play()
	{
		if (busy || !IsInstalled(state.installDirectory))
		{
			return;
		}
		if (latest != null)
		{
			Version current = InstalledVersion();
			if (current != null && latest.Version > current && MessageBox.Show(this, "Доступна версия " + latest.Version.ToString(3) + ". Обновиться перед запуском? Все игроки должны быть на одной версии.", Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
			{
				await InstallOrUpdate();
			}
		}
		if (Process.GetProcessesByName("steam").Length == 0)
		{
			MessageBox.Show(this, "Сначала запустите Steam и войдите в аккаунт.", Text);
			return;
		}
		SetBusy(true, "Запускаю игру…");
		try
		{
			await RunScript(Path.Combine(state.installDirectory, "Launch-Coop.ps1"), "");
			Log("Игра запущена.");
		}
		catch (Exception ex)
		{
			Log("ОШИБКА: " + ex.Message);
			MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		finally
		{
			SetBusy(false, "Игра запущена. Лаунчер можно закрыть.");
		}
	}

	private bool GameRunning()
	{
		string exe = Path.Combine(state.installDirectory, "game", "tld.exe");
		foreach (Process process in Process.GetProcessesByName("tld"))
		{
			try
			{
				if (string.Equals(process.MainModule.FileName, exe, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			catch
			{
				return true;
			}
		}
		return false;
	}

	private Task RunScript(string script, string arguments)
	{
		return Task.Run(delegate
		{
			Log("> " + Path.GetFileName(script) + " " + arguments);
			ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" " + arguments)
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8,
				WorkingDirectory = Path.GetDirectoryName(script)
			};
			StringBuilder errors = new StringBuilder();
			using Process process = new Process { StartInfo = info };
			process.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (!string.IsNullOrWhiteSpace(e.Data))
				{
					Log(e.Data);
				}
			};
			process.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (!string.IsNullOrWhiteSpace(e.Data))
				{
					errors.AppendLine(e.Data);
					Log(e.Data);
				}
			};
			process.Start();
			process.BeginOutputReadLine();
			process.BeginErrorReadLine();
			process.WaitForExit();
			if (process.ExitCode != 0)
			{
				string message = ExtractError(errors.ToString());
				throw new InvalidOperationException(string.IsNullOrEmpty(message) ? (Path.GetFileName(script) + " завершился с ошибкой " + process.ExitCode) : message);
			}
		});
	}

	// The scripts end with "throw '...'"; show that text instead of PowerShell's whole error record.
	private static string ExtractError(string stderr)
	{
		foreach (string line in stderr.Split('\n'))
		{
			string trimmed = line.Trim();
			if (trimmed.Length > 0 && !trimmed.StartsWith("At ", StringComparison.Ordinal) && !trimmed.StartsWith("+", StringComparison.Ordinal) && !trimmed.StartsWith("CategoryInfo", StringComparison.Ordinal) && !trimmed.StartsWith("FullyQualifiedErrorId", StringComparison.Ordinal))
			{
				return trimmed;
			}
		}
		return stderr.Trim();
	}

	// Non-interactive check used by the build: GitHub release lookup and game discovery.
	public static string SelfCheck()
	{
		StringBuilder text = new StringBuilder();
		ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
		try
		{
			ReleaseInfo stable = FetchLatest(includeTest: false);
			ReleaseInfo test = FetchLatest(includeTest: true);
			text.AppendLine("stable=" + (stable?.Tag ?? "none") + " test=" + (test?.Tag ?? "none") + " zip=" + (test?.ZipName ?? "") + " launcherAsset=" + (string.IsNullOrEmpty(test?.LauncherUrl) ? "no" : "yes"));
		}
		catch (Exception ex)
		{
			text.AppendLine("github error: " + ex.Message);
		}
		text.AppendLine("steamGame=" + (FindSteamGame() ?? "not found"));
		return text.ToString();
	}

	// ---------------------------------------------------------------- finding the game

	private static string FindSteamGame()
	{
		List<string> libraries = new List<string>();
		foreach (string key in new string[3] { "HKEY_CURRENT_USER\\Software\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\Valve\\Steam", "HKEY_LOCAL_MACHINE\\SOFTWARE\\Valve\\Steam" })
		{
			foreach (string name in new string[2] { "SteamPath", "InstallPath" })
			{
				if (Registry.GetValue(key, name, null) is string path && Directory.Exists(path))
				{
					libraries.Add(path.Replace('/', '\\'));
				}
			}
		}
		foreach (string steam in libraries.ToArray())
		{
			string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
			if (!File.Exists(vdf))
			{
				continue;
			}
			foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
			{
				libraries.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
			}
		}
		foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			string game = Path.Combine(library, "steamapps", "common", "TheLongDark");
			if (File.Exists(Path.Combine(game, "tld.exe")) && !Directory.Exists(Path.Combine(game, "MelonLoader")))
			{
				return game;
			}
		}
		return null;
	}

	private string AskFolder(string description, string requiredFile)
	{
		using FolderBrowserDialog dialog = new FolderBrowserDialog { Description = description, ShowNewFolderButton = false };
		while (dialog.ShowDialog(this) == DialogResult.OK)
		{
			if (File.Exists(Path.Combine(dialog.SelectedPath, requiredFile)))
			{
				return dialog.SelectedPath;
			}
			MessageBox.Show(this, "В этой папке нет " + requiredFile + ". Выберите папку игры.", Text);
		}
		return null;
	}
}
