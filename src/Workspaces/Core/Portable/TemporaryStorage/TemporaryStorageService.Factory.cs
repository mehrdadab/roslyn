﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Composition;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Host
{
    internal partial class TemporaryStorageService
    {
        [ExportWorkspaceServiceFactory(typeof(ITemporaryStorageService), ServiceLayer.Default), Shared]
        internal partial class Factory : IWorkspaceServiceFactory
        {
            private readonly IWorkspaceThreadingService? _workspaceThreadingService;

            [ImportingConstructor]
            [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
            public Factory(
                [Import(AllowDefault = true)] IWorkspaceThreadingService? workspaceThreadingService)
            {
                _workspaceThreadingService = workspaceThreadingService;
            }

            [Obsolete(MefConstruction.FactoryMethodMessage, error: true)]
            public IWorkspaceService CreateService(HostWorkspaceServices workspaceServices)
            {
                var textFactory = workspaceServices.GetRequiredService<ITextFactoryService>();

                // MemoryMapped files which are used by the TemporaryStorageService are present in .NET Framework (including Mono)
                // and .NET Core Windows. For non-Windows .NET Core scenarios, we can return the TrivialTemporaryStorageService
                // until https://github.com/dotnet/runtime/issues/30878 is fixed.
                return PlatformInformation.IsWindows || PlatformInformation.IsRunningOnMono
                    ? new TemporaryStorageService(_workspaceThreadingService, textFactory)
                    : TrivialTemporaryStorageService.Instance;
            }
        }
    }
}
