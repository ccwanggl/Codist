﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using CLR;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;
using R = Codist.Properties.Resources;

namespace Codist
{
	/// <summary>
	/// <para>This is the class that implements the package exposed by <see cref="Codist"/>.</para>
	/// <para>The project consists of the following namespace: <see cref="SyntaxHighlight"/> backed by <see cref="Taggers"/>, <see cref="SmartBars"/>, <see cref="QuickInfo"/>, <see cref="Margins"/>, <see cref="NaviBar"/> etc.</para>
	/// </summary>
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration("#110", "#112", Config.CurrentVersion, IconResourceID = 400)] // Information on this package for Help/About
	[Guid(PackageGuidString)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	//[ProvideToolWindow(typeof(Commands.SymbolFinderWindow), Style = VsDockStyle.Tabbed, Window = EnvDTE.Constants.vsWindowKindProperties))]
	[ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
	sealed class CodistPackage : AsyncPackage
	{
		/// <summary>CodistPackage GUID string. Should be the same as the one in <c>source.extension.vsixmanifest</c>.</summary>
		internal const string PackageGuidString = "c7b93d20-621f-4b21-9d28-d51157ef0b94";

		static OleMenuCommandService __Menu;

		//int _extenderCookie;

		/// <summary>
		/// Initializes a new instance of the <see cref="CodistPackage"/> class.
		/// </summary>
		public CodistPackage() {
			// Inside this method you can place any initialization code that does not require
			// any Visual Studio service because at this point the package object is created but
			// not sited yet inside Visual Studio environment. The place to do all the other
			// initialization is the Initialize method.
			Instance = this;
			$"{nameof(CodistPackage)} created".Log();
		}

		public static CodistPackage Instance { get; private set; }
		public static Version VsVersion => Vs.Version;
		public static EnvDTE80.DTE2 DTE { get; } = GetGlobalService(typeof(SDTE)) as EnvDTE80.DTE2;

		public static OleMenuCommandService MenuService {
			get {
				ThreadHelper.ThrowIfNotOnUIThread();
				return __Menu ?? (__Menu = Instance.GetService(typeof(System.ComponentModel.Design.IMenuCommandService)) as OleMenuCommandService);
			}
		}

		public static DebuggerStatus DebuggerStatus {
			get {
				ThreadHelper.ThrowIfNotOnUIThread(nameof(DebuggerStatus));
				switch (DTE.Debugger.CurrentMode) {
					case EnvDTE.dbgDebugMode.dbgBreakMode: return DebuggerStatus.Break;
					case EnvDTE.dbgDebugMode.dbgDesignMode: return DebuggerStatus.Design;
					case EnvDTE.dbgDebugMode.dbgRunMode: return DebuggerStatus.Running;
				}
				return DebuggerStatus.Design;
			}
		}

		public static void OpenWebPage(InitStatus status) {
			switch (status) {
				case InitStatus.FirstLoad:
					ExternalCommand.OpenWithWebBrowser("https://github.com/wmjordan/Codist");
					break;
				case InitStatus.Upgraded:
					ExternalCommand.OpenWithWebBrowser("https://github.com/wmjordan/Codist/releases");
					break;
			}
		}

		public static void ShowError(Exception ex, string title) {
			if (ThreadHelper.CheckAccess()) {
				Controls.MessageWindow.Error(ex, title);
			}
			else {
				ShowErrorAsync(ex, title).FireAndForget();
			}
		}

		static async Task ShowErrorAsync(Exception error, string title) {
			await SyncHelper.SwitchToMainThreadAsync();
			Controls.MessageWindow.Error(error, title);
		}

		#region Package Members
		/// <summary>
		/// Initialization of the package; this method is called right after the package is sited, so this is the place
		/// where you can put all the initialization code that rely on services provided by VisualStudio.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
		/// <param name="progress">A provider for progress updates.</param>
		/// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) {
			"Initializing package".Log();

			await base.InitializeAsync(cancellationToken, progress);

			SolutionEvents.OnAfterCloseSolution += (s, args) => Taggers.SymbolMarkManager.Clear();

			// implicitly load config here, before switching to the main thread
			var config = Config.Instance;

			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			"Package initialization switched to main thread".Log();
			//_extenderCookie = DTE.ObjectExtenders.RegisterExtenderProvider(VSConstants.CATID.CSharpFileProperties_string, BuildBots.AutoReplaceExtenderProvider.Name, new BuildBots.AutoReplaceExtenderProvider());
			Commands.CommandRegistry.Initialize();
			Display.JumpListEnhancer.Initialize();
			Display.LayoutOverride.Initialize();
			if (config.DisplayOptimizations != DisplayOptimizations.None) {
				Display.ResourceMonitor.Reload(config.DisplayOptimizations);
			}
			//await Commands.FavoritesWindowCommand.InitializeAsync(this);

			if (config.InitStatus != InitStatus.Normal) {
				InitializeOrUpgradeConfig();
				"Config upgraded.".Log();
			}
			Commands.SyntaxCustomizerWindowCommand.Initialize();
			Commands.OptionsWindowCommand.Initialize();

			"Package initialization finished.".Log();
			//ListEditorCommands();
		}

		void InitializeOrUpgradeConfig() {
			try {
				// save the file to prevent following notification showing up again until future upgrade
				Config.Instance.SaveConfig(Config.ConfigPath);
			}
			catch (Exception ex) {
				Controls.MessageWindow.Error(ex, R.T_ErrorSavingConfig);
			}

			try {
				new Commands.VersionInfoBar(this).Show(Config.Instance.InitStatus);
			}
			catch (MissingMemberException) {
				// HACK: For VS 2022, InfoBar is broken. Prompt to open page at this moment.
				string welcome = Config.Instance.InitStatus.Case(InitStatus.FirstLoad, R.T_FirstRunPrompt, R.T_NewVersionPrompt);
				if (new Controls.MessageWindow(welcome, null, MessageBoxButton.YesNo, MessageBoxImage.Information) { Topmost = true }.ShowDialog() == true) {
					OpenWebPage(Config.Instance.InitStatus);
				}
			}
		}

		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			LogHelper.ClearLog();
			//DTE.ObjectExtenders.UnregisterExtenderProvider(_extenderCookie);
		}

#pragma warning disable VSTHRD010 // Invoke single-threaded types on Main thread
		/// <summary>A helper method to discover registered editor commands.</summary>
		static void ListEditorCommands() {
			var commands = DTE.Commands;
			var c = commands.Count;
			var s = new string[c];
			var s2 = new string[c];
			var d = new HashSet<string>();
			for (int i = 0; i < s.Length; i++) {
				var name = commands.Item(i+1).Name;
				if (d.Add(name) == false) {
					continue;
				}
				if (name.IndexOf('.') == -1) {
					s[i] = name;
				}
				else {
					s2[i] = name;
				}
			}
			Array.Sort(s);
			Array.Sort(s2);
			var sb = new System.Text.StringBuilder(16000)
				.AppendLine("// Call CodistPackage.ListEditorCommands to generate this file")
				.AppendLine();
			foreach (var item in s) {
				if (String.IsNullOrEmpty(item) == false) {
					sb.AppendLine(item);
				}
			}
			foreach (var item in s2) {
				if (String.IsNullOrEmpty(item) == false) {
					sb.AppendLine(item);
				}
			}
			Debug.WriteLine(sb);
		}
		#endregion

		static class Vs
		{
			public static readonly Version Version = new Version(Application.Current.MainWindow.GetType().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
		}
	}
#pragma warning restore VSTHRD010 // Invoke single-threaded types on Main thread
}
