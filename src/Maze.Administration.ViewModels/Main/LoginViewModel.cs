using System;
using System.Linq;
using System.Security;
using System.Threading;
using NuGet.Frameworks;
using Maze.Administration.Core;
using Maze.Administration.Core.Modules;
using Maze.Administration.Core.Rest;
using Maze.Administration.Library.Exceptions;
using Maze.Administration.Library.Rest.Modules.V1;
using Maze.Administration.ViewModels.Utilities;
using Maze.ModuleManagement;
using Prism.Mvvm;
using Unclassified.TxLib;

namespace Maze.Administration.ViewModels.Main
{
    public class LoginViewModel : BindableBase
    {
        private static readonly NuGetFramework
            Framework = FrameworkConstants.CommonFrameworks.MazeAdministration10; //TODO move somewhere else

        private readonly Action<AppLoadContext> _loadAppAction;
        private string _errorMessage;
        private bool _isLoggingIn;
        private AsyncRelayCommand<SecureString> _loginCommand;
        private string _statusMessage;
        private string _username;

        public LoginViewModel(Action<AppLoadContext> loadAppAction)
        {
            _loadAppAction = loadAppAction;
        }

        public bool IsLoggingIn
        {
            get => _isLoggingIn;
            set => SetProperty(ref _isLoggingIn, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set => SetProperty(ref _errorMessage, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public AsyncRelayCommand<SecureString> LoginCommand
        {
            get
            {
                return _loginCommand ?? (_loginCommand = new AsyncRelayCommand<SecureString>(async parameter =>
                {
                    IsLoggingIn = true;

                    try
                    {
                        StatusMessage = Tx.T("LoginView:Status.Authenticating");

                        var serverInfo = new ServerInfo {ServerUri = new Uri("http://localhost:54941/") };
                        var client = await MazeRestConnector.TryConnect(Username, parameter, serverInfo);

                        StatusMessage = Tx.T("LoginView:Status.RetrieveModules");

                        var modules = await ModulesResource.FetchModules(Framework, client);

                        var options = new ModulesOptions {LocalPath = "packages", TempPath = "temp"};
                        var directory = new ModulesDirectory(
                            new VersionFolderPathResolverFlat(
                                Environment.ExpandEnvironmentVariables(options.LocalPath)));
                        var catalog = new ModulesCatalog(directory, Framework);

                        if (modules.Any())
                        {
                            StatusMessage = Tx.T("LoginView:Status.DownloadModules");

                            var downloader = new ModuleDownloader(directory, options);
                            await downloader.Load(modules, serverInfo, CancellationToken.None);

                            StatusMessage = Tx.T("LoginView:Status.LoadModules");

                            await catalog.Load(modules);
                        }

                        _loadAppAction(new AppLoadContext {ModulesCatalog = catalog, RestClient = client});
                    }
                    catch (RestAuthenticationException e)
                    {
                        //ErrorMessage = e.GetRestExceptionMessage();
                        IsLoggingIn = false;
                    }
                    catch (Exception e)
                    {
                        ErrorMessage = e.Message;
                        IsLoggingIn = false;
                        //e.ShowMessageBox();
                    }
                }));
            }
        }
    }
}