﻿using Orcus.Administration.Core.Clients;

namespace Orcus.Administration.Core.Modules
{
    public class AppLoadContext
    {
        public IModulesCatalog ModulesCatalog { get; set; }
        public IOrcusRestClient RestClient { get; set; }
    }
}