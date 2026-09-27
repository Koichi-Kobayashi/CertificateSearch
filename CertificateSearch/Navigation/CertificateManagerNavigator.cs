// Copyright (c) 2026 Koichi Kobayashi
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Windows.Automation;
using CertificateSearch.Certificates;

namespace CertificateSearch.Navigation;

internal static class CertificateManagerNavigator
{
    private const int PollMilliseconds = 150;
    private const int TimeoutMilliseconds = 12000;

    // MMC's display names are localized. Internal names are tried first; these are
    // a best-effort English fallback, never a reason to choose a different store.
    private static readonly System.Collections.Generic.Dictionary<string, string> StoreLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["My"] = "Personal",
        ["Root"] = "Trusted Root Certification Authorities",
        ["CA"] = "Intermediate Certification Authorities",
        ["AuthRoot"] = "Third-Party Root Certification Authorities",
        ["TrustedPeople"] = "Trusted People",
        ["TrustedPublisher"] = "Trusted Publishers",
        ["Disallowed"] = "Untrusted Certificates",
        ["AddressBook"] = "Other People",
    };

    public static void Open(CertificateEntry entry, bool selectCertificate, bool allowMetadataMatch = false)
    {
        var process = Start(entry.StoreLocation);
        var thread = new Thread(() => Navigate(process, entry, selectCertificate, allowMetadataMatch))
        {
            IsBackground = true,
            Name = "Navigate certificate manager",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public static void OpenStore(CertificateEntry entry) => Open(entry, false);

    public static void NavigateAndWait(CertificateEntry entry, bool selectCertificate, bool allowMetadataMatch)
    {
        var process = Start(entry.StoreLocation);
        var thread = new Thread(() => Navigate(process, entry, selectCertificate, allowMetadataMatch))
        {
            IsBackground = true,
            Name = "Navigate certificate manager",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static Process Start(StoreLocation location)
    {
        var console = location == StoreLocation.LocalMachine ? "certlm.msc" : "certmgr.msc";
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "mmc.exe"),
            Arguments = $"\"{Path.Combine(Environment.SystemDirectory, console)}\"",
            // Shell execution can elevate certmgr.msc even when no UAC prompt is
            // shown. A direct launch keeps Current User MMC at our integrity level.
            UseShellExecute = location == StoreLocation.LocalMachine,
        };

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Certificate manager could not be started.");
    }

    private static void Navigate(Process process, CertificateEntry entry, bool selectCertificate, bool allowMetadataMatch)
    {
        using (process)
        {
            try
            {
                var window = WaitForWindow(process);
                if (window is null)
                {
                    NavigationDiagnostics.Write("Navigator: MMC main window unavailable");
                    Trace.TraceWarning("Certificate manager window was not found.");
                    return;
                }

                NavigationDiagnostics.Write("Navigator: MMC main window available");
                BringToFront(window);

                for (var attempt = 0; attempt < 5; attempt++)
                {
                    var store = WaitForStore(process, entry.StoreName);
                    if (store is null)
                    {
                        NavigationDiagnostics.Write("Navigator: requested store tree item unavailable");
                        Trace.TraceWarning($"Certificate manager store was not found: {entry.Location}");
                        return;
                    }

                    NavigationDiagnostics.Write("Navigator: requested store tree item found");
                    try
                    {
                        Expand(store);
                        // A store's Certificates child is its list view. Prefer a named child,
                        // but the only child is safe even in localized MMC installations.
                        var child = WaitForCertificatesChild(store);
                        Select(child ?? store);
                        NavigationDiagnostics.Write(child is null ?
                            "Navigator: selected store; certificate child unavailable" :
                            "Navigator: selected certificate list node");

                        if (selectCertificate && child is not null)
                        {
                            TrySelectUniqueCertificate(process, entry, allowMetadataMatch);
                        }

                        return;
                    }
                    catch (Exception exception) when (exception is ElementNotAvailableException or COMException)
                    {
                        NavigationDiagnostics.Write($"Navigator: store node was recreated; retrying ({exception.GetType().Name}, 0x{exception.HResult:X8})");
                    }
                }

                NavigationDiagnostics.Write("Navigator: store node remained unavailable after retries");
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or InvalidOperationException or COMException or Win32Exception)
            {
                NavigationDiagnostics.Write($"Navigator: failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
                Trace.TraceWarning($"Certificate manager navigation: {exception}");
            }
        }
    }

    private static AutomationElement? WaitForWindow(Process process)
    {
        for (var elapsed = 0; elapsed < TimeoutMilliseconds; elapsed += PollMilliseconds)
        {
            if (process.HasExited)
            {
                return null;
            }

            var window = FindMmcWindow(process);
            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(PollMilliseconds);
        }

        return null;
    }

    private static AutomationElement? FindMmcWindow(Process process)
    {
        // MainWindowHandle can point to MMC's separate UAC Input Indicator pane.
        // Select the actual console frame among this process's top-level windows.
        var windows = AutomationElement.RootElement.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
        return windows.Cast<AutomationElement>().FirstOrDefault(window =>
            string.Equals(window.Current.ClassName, "MMCMainFrame", StringComparison.Ordinal));
    }

    private static void BringToFront(AutomationElement window)
    {
        var handle = new IntPtr(window.Current.NativeWindowHandle);
        if (handle == IntPtr.Zero)
        {
            NavigationDiagnostics.Write("Navigator: MMC frame has no native window handle");
            return;
        }

        const uint showWithoutMovingOrResizing = 0x0040 | 0x0001 | 0x0002;
        var raised = SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, showWithoutMovingOrResizing);
        var focused = SetForegroundWindow(handle);
        if (!focused)
        {
            try
            {
                window.SetFocus();
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or COMException or InvalidOperationException)
            {
                NavigationDiagnostics.Write($"Navigator: MMC focus fallback failed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
            }
        }

        NavigationDiagnostics.Write($"Navigator: MMC raised={raised}; foreground requested={focused}; active={GetForegroundWindow() == handle}");
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static AutomationElement? WaitForStore(Process process, string storeName)
    {
        var localizedName = StoreDisplayName.GetLocalizedName(storeName);
        var stopwatch = Stopwatch.StartNew();
        var treeFound = false;
        var transientFailures = 0;
        while (stopwatch.ElapsedMilliseconds < TimeoutMilliseconds)
        {
            try
            {
                if (process.HasExited)
                {
                    NavigationDiagnostics.Write("Navigator: launched MMC process exited before store discovery");
                    return null;
                }

                var window = FindMmcWindow(process);
                if (window is null)
                {
                    Thread.Sleep(PollMilliseconds);
                    continue;
                }

                var tree = window.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tree));
                if (tree is not null && !treeFound)
                {
                    treeFound = true;
                    NavigationDiagnostics.Write("Navigator: MMC navigation tree found");
                }

                // MMC stores are immediate children of the console root. Searching
                // the whole window for all matches is expensive in UI Automation.
                if (tree is not null)
                {
                    var roots = tree.FindAll(TreeScope.Children,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
                    foreach (AutomationElement root in roots)
                    {
                        Expand(root);
                        var nodes = root.FindAll(TreeScope.Children,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
                        var matches = nodes.Cast<AutomationElement>().Where(node =>
                            string.Equals(node.Current.AutomationId, storeName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(node.Current.Name, storeName, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(localizedName) &&
                             string.Equals(node.Current.Name, localizedName, StringComparison.OrdinalIgnoreCase)) ||
                            (StoreLabels.TryGetValue(storeName, out var label) &&
                             string.Equals(node.Current.Name, label, StringComparison.OrdinalIgnoreCase)))
                            .GroupBy(RuntimeId)
                            .Select(group => group.First())
                            .ToArray();
                        if (matches.Length == 1)
                        {
                            return matches[0];
                        }

                        if (matches.Length > 1)
                        {
                            NavigationDiagnostics.Write($"Navigator: {matches.Length} distinct matching store tree items");
                            return null;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or COMException)
            {
                transientFailures++;
                if (transientFailures <= 3)
                {
                    NavigationDiagnostics.Write($"Navigator: transient store-discovery failure ({exception.GetType().Name}, 0x{exception.HResult:X8})");
                }
            }

            Thread.Sleep(PollMilliseconds);
        }

        NavigationDiagnostics.Write($"Navigator: store discovery timed out; navigation tree found={treeFound}; transient failures={transientFailures}");
        return null;
    }

    private static string RuntimeId(AutomationElement element) =>
        string.Join(".", element.GetRuntimeId());

    private static void Expand(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern) &&
            ((ExpandCollapsePattern)pattern).Current.ExpandCollapseState == ExpandCollapseState.Collapsed)
        {
            ((ExpandCollapsePattern)pattern).Expand();
        }
    }

    private static AutomationElement? WaitForCertificatesChild(AutomationElement store)
    {
        for (var elapsed = 0; elapsed < 3000; elapsed += PollMilliseconds)
        {
            var children = store.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
            var named = children.Cast<AutomationElement>()
                .FirstOrDefault(node => string.Equals(node.Current.Name, "Certificates", StringComparison.OrdinalIgnoreCase));
            if (named is not null)
            {
                return named;
            }

            if (children.Count == 1)
            {
                return children[0];
            }

            Thread.Sleep(PollMilliseconds);
        }

        return null;
    }

    private static void Select(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
        {
            ((SelectionItemPattern)pattern).Select();
        }
    }

    private static void TrySelectUniqueCertificate(Process process, CertificateEntry entry, bool allowMetadataMatch)
    {
        for (var elapsed = 0; elapsed < 5000; elapsed += PollMilliseconds)
        {
            try
            {
                var window = FindMmcWindow(process);
                if (window is not null)
                {
                    var list = window.FindFirst(TreeScope.Descendants,
                        new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table)));
                    if (list is null)
                    {
                        Thread.Sleep(PollMilliseconds);
                        continue;
                    }

                    var rows = list.FindAll(TreeScope.Children,
                        new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)));
                    var rowElements = rows.Cast<AutomationElement>()
                        .GroupBy(RuntimeId)
                        .Select(group => group.First())
                        .ToArray();
                    var rowNames = rowElements.Select(row => row.Current.Name).ToArray();
                    var matchesByName = CertificateRowMatcher.FindMatchingRows(
                        rowNames.Select(name => new[] { name }).ToArray(), entry, allowMetadataMatch);
                    if (matchesByName.Length == 1)
                    {
                        NavigationDiagnostics.Write("Navigator: unique certificate row found by row name");
                        SelectAndOpen(rowElements[matchesByName[0]]);
                        return;
                    }

                    if (allowMetadataMatch && !string.IsNullOrWhiteSpace(entry.CommonName))
                    {
                        var candidateIndexes = Enumerable.Range(0, rowElements.Length)
                            .Where(index => rowNames[index].Contains(entry.CommonName, StringComparison.OrdinalIgnoreCase))
                            .ToArray();
                        if (candidateIndexes.Length > 0 && candidateIndexes.Length < rowElements.Length)
                        {
                            var candidateMatches = CertificateRowMatcher.FindMatchingRows(
                                candidateIndexes.Select(index => RowFields(rowElements[index], TreeScope.Children)).ToArray(),
                                entry, true);
                            if (candidateMatches.Length == 1)
                            {
                                NavigationDiagnostics.Write($"Navigator: unique certificate row found among {candidateIndexes.Length} candidates");
                                SelectAndOpen(rowElements[candidateIndexes[candidateMatches[0]]]);
                                return;
                            }
                        }
                    }

                    if (allowMetadataMatch)
                    {
                        var shallowMatches = CertificateRowMatcher.FindMatchingRows(
                            rowElements.Select(row => RowFields(row, TreeScope.Children)).ToArray(),
                            entry, true);
                        if (shallowMatches.Length == 1)
                        {
                            NavigationDiagnostics.Write("Navigator: unique certificate row found in direct child fields");
                            SelectAndOpen(rowElements[shallowMatches[0]]);
                            return;
                        }
                    }

                    var matches = CertificateRowMatcher.FindMatchingRows(
                        rowElements.Select(row => RowFields(row, TreeScope.Descendants)).ToArray(),
                        entry, allowMetadataMatch);
                    if (matches.Length == 1)
                    {
                        NavigationDiagnostics.Write("Navigator: unique certificate row found");
                        SelectAndOpen(rowElements[matches[0]]);
                        return;
                    }

                    if (matches.Length > 1)
                    {
                        NavigationDiagnostics.Write($"Navigator: {matches.Length} matching rows; selection skipped");
                        Trace.TraceWarning("Certificate manager returned ambiguous matching rows.");
                        return;
                    }
                }
            }
            catch (Exception exception) when (exception is ElementNotAvailableException or COMException)
            {
                NavigationDiagnostics.Write($"Navigator: certificate list refreshed ({exception.GetType().Name}, 0x{exception.HResult:X8})");
            }

            Thread.Sleep(PollMilliseconds);
        }

        Trace.TraceInformation("Certificate manager did not expose an identifiable certificate row.");
        NavigationDiagnostics.Write("Navigator: no identifiable certificate row");
    }

    private static void SelectAndOpen(AutomationElement match)
    {
        if (match.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var scroll))
        {
            ((ScrollItemPattern)scroll).ScrollIntoView();
        }

        Select(match);
        if (match.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke))
        {
            ((InvokePattern)invoke).Invoke();
        }
    }

    private static string[] RowFields(AutomationElement row, TreeScope scope)
    {
        var children = row.FindAll(scope, Condition.TrueCondition);
        return new[] { row.Current.Name }.Concat(children.Cast<AutomationElement>().Select(x => x.Current.Name)).ToArray();
    }
}
