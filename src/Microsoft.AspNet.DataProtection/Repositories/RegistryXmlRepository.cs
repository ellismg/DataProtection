﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Framework.Internal;
using Microsoft.Framework.Logging;
using Microsoft.Win32;

using static System.FormattableString;

namespace Microsoft.AspNet.DataProtection.Repositories
{
    /// <summary>
    /// An XML repository backed by the Windows registry.
    /// </summary>
    public class RegistryXmlRepository : IXmlRepository
    {
        private static readonly Lazy<RegistryKey> _defaultRegistryKeyLazy = new Lazy<RegistryKey>(GetDefaultHklmStorageKey);

        private readonly ILogger _logger;

        /// <summary>
        /// Creates a <see cref="RegistryXmlRepository"/> with keys stored in the given registry key.
        /// </summary>
        /// <param name="registryKey">The registry key in which to persist key material.</param>
        public RegistryXmlRepository([NotNull] RegistryKey registryKey)
            : this(registryKey, services: null)
        {
        }

        /// <summary>
        /// Creates a <see cref="RegistryXmlRepository"/> with keys stored in the given registry key.
        /// </summary>
        /// <param name="registryKey">The registry key in which to persist key material.</param>
        public RegistryXmlRepository([NotNull] RegistryKey registryKey, IServiceProvider services)
        {
            RegistryKey = registryKey;
            Services = services;
            _logger = services?.GetLogger<RegistryXmlRepository>();
        }

        /// <summary>
        /// The default key storage directory, which currently corresponds to
        /// "HKLM\SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys\{SID}".
        /// </summary>
        /// <remarks>
        /// This property can return null if no suitable default registry key can
        /// be found, such as the case when this application is not hosted inside IIS.
        /// </remarks>
        public static RegistryKey DefaultRegistryKey => _defaultRegistryKeyLazy.Value;

        /// <summary>
        /// The registry key into which key material will be written.
        /// </summary>
        public RegistryKey RegistryKey { get; }

        /// <summary>
        /// The <see cref="IServiceProvider"/> provided to the constructor.
        /// </summary>
        protected IServiceProvider Services { get; }

        public virtual IReadOnlyCollection<XElement> GetAllElements()
        {
            // forces complete enumeration
            return GetAllElementsCore().ToList().AsReadOnly();
        }

        private IEnumerable<XElement> GetAllElementsCore()
        {
            // Note: Inability to parse any value is considered a fatal error (since the value may contain
            // revocation information), and we'll fail the entire operation rather than return a partial
            // set of elements. If a file contains well-formed XML but its contents are meaningless, we
            // won't fail that operation here. The caller is responsible for failing as appropriate given
            // that scenario.

            foreach (string valueName in RegistryKey.GetValueNames())
            {
                XElement element = ReadElementFromRegKey(RegistryKey, valueName);
                if (element != null)
                {
                    yield return element;
                }
            }
        }

        private static RegistryKey GetDefaultHklmStorageKey()
        {
            try
            {
                var registryView = IntPtr.Size == 4 ? RegistryView.Registry32 : RegistryView.Registry64;
                // Try reading the auto-generated machine key from HKLM
                using (var hklmBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
                {
                    // Even though this is in HKLM, WAS ensures that applications hosted in IIS are properly isolated.
                    // See APP_POOL::EnsureSharedMachineKeyStorage in WAS source for more info.
                    // The version number will need to change if IIS hosts Core CLR directly.
                    string aspnetAutoGenKeysBaseKeyName = Invariant($@"SOFTWARE\Microsoft\ASP.NET\4.0.30319.0\AutoGenKeys\{WindowsIdentity.GetCurrent().User.Value}");
                    var aspnetBaseKey = hklmBaseKey.OpenSubKey(aspnetAutoGenKeysBaseKeyName, writable: true);
                    if (aspnetBaseKey != null)
                    {
                        using (aspnetBaseKey)
                        {
                            // We'll create a 'DataProtection' subkey under the auto-gen keys base
                            return aspnetBaseKey.OpenSubKey("DataProtection", writable: true)
                                ?? aspnetBaseKey.CreateSubKey("DataProtection");
                        }
                    }
                    return null; // couldn't find the auto-generated machine key
                }
            }
            catch
            {
                // swallow all errors; they're not fatal
                return null;
            }
        }

        private static bool IsSafeRegistryValueName(string filename)
        {
            // Must be non-empty and contain only a-zA-Z0-9, hyphen, and underscore.
            return (!String.IsNullOrEmpty(filename) && filename.All(c =>
                c == '-'
                || c == '_'
                || ('0' <= c && c <= '9')
                || ('A' <= c && c <= 'Z')
                || ('a' <= c && c <= 'z')));
        }

        private XElement ReadElementFromRegKey(RegistryKey regKey, string valueName)
        {
            if (_logger.IsVerboseLevelEnabled())
            {
                _logger.LogVerboseF($"Reading data from registry key '{regKey}', value '{valueName}'.");
            }

            string data = regKey.GetValue(valueName) as string;
            return (!String.IsNullOrEmpty(data)) ? XElement.Parse(data) : null;
        }

        public virtual void StoreElement([NotNull] XElement element, string friendlyName)
        {
            if (!IsSafeRegistryValueName(friendlyName))
            {
                string newFriendlyName = Guid.NewGuid().ToString();
                if (_logger.IsVerboseLevelEnabled())
                {
                    _logger.LogVerboseF($"The name '{friendlyName}' is not a safe registry value name, using '{newFriendlyName}' instead.");
                }
                friendlyName = newFriendlyName;
            }

            StoreElementCore(element, friendlyName);
        }

        private void StoreElementCore(XElement element, string valueName)
        {
            // Technically calls to RegSetValue* and RegGetValue* are atomic, so we don't have to worry about
            // another thread trying to read this value while we're writing it. There's still a small risk of
            // data corruption if power is lost while the registry file is being flushed to the file system,
            // but the window for that should be small enough that we shouldn't have to worry about it.
            RegistryKey.SetValue(valueName, element.ToString(), RegistryValueKind.String);
        }
    }
}
