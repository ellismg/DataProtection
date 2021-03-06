﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNet.Cryptography.Cng;
using Microsoft.AspNet.Testing.xunit;

namespace Microsoft.AspNet.DataProtection.Test.Shared
{
    public class ConditionalRunTestOnlyOnWindowsAttribute : Attribute, ITestCondition
    {
        public bool IsMet => OSVersionUtil.IsWindows();

        public string SkipReason { get; } = "Test requires Windows 7 / Windows Server 2008 R2 or higher.";
    }
}
