﻿using Orcus.Administration.Library.Clients;
using Orcus.Administration.Library.Extensions;
using Orcus.Administration.Library.StatusBar;
using Orcus.Administration.Library.ViewModels;

namespace SystemInformation.Administration.ViewModels
{
    public class SystemInformationViewModel : ViewModelBase
    {
        private readonly IShellStatusBar _statusBar;
        private readonly IPackageRestClient _restClient;

        public SystemInformationViewModel(IShellStatusBar statusBar, ITargetedRestClient restClient)
        {
            _statusBar = statusBar;
            _restClient = restClient.CreatePackageSpecific("SystemInformation");
        }
    }
}