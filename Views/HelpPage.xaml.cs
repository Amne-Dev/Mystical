using Mystical.WinUI.Models;

namespace Mystical.WinUI.Views;

public sealed partial class HelpPage : Page
{
    private readonly Dictionary<string, HelpDocSection> _sections;
    private readonly Dictionary<string, TreeViewNode> _sectionNodes;
    private readonly Dictionary<string, string> _titleToSectionKey;
    private bool _suppressSelectionNavigation;

    public HelpPage()
    {
        InitializeComponent();
        _sections = BuildSections();
        _sectionNodes = new Dictionary<string, TreeViewNode>(StringComparer.OrdinalIgnoreCase);
        _titleToSectionKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        BuildHelpTree();
        NavigateToSection("start");
    }

    public void NavigateToSection(string sectionKey)
    {
        NavigateToSectionInternal(sectionKey, syncTreeSelection: true);
    }

    private void NavigateToSectionInternal(string sectionKey, bool syncTreeSelection)
    {
        if (!_sections.TryGetValue(sectionKey, out var section))
        {
            section = _sections["start"];
            sectionKey = section.SectionKey;
        }

        if (syncTreeSelection && _sectionNodes.TryGetValue(sectionKey, out var targetNode))
        {
            _suppressSelectionNavigation = true;
            HelpTree.SelectedNode = targetNode;
            _suppressSelectionNavigation = false;
        }

        _ = SectionFrame.Navigate(typeof(HelpSectionPage), section);
    }

    private void OnHelpTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        try
        {
            var sectionKey = ResolveSectionKey(args.InvokedItem);
            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                return;
            }

            NavigateToSectionInternal(sectionKey, syncTreeSelection: false);
        }
        catch
        {
            // Prevent UI crashes from unexpected TreeView payload shapes.
        }
    }

    private void OnHelpTreeSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        try
        {
            if (_suppressSelectionNavigation || args.AddedItems.Count == 0)
            {
                return;
            }

            var sectionKey = ResolveSectionKey(args.AddedItems[0]);
            if (string.IsNullOrWhiteSpace(sectionKey))
            {
                return;
            }

            NavigateToSectionInternal(sectionKey, syncTreeSelection: false);
        }
        catch
        {
            // Prevent UI crashes from unexpected TreeView payload shapes.
        }
    }

    private void BuildHelpTree()
    {
        HelpTree.RootNodes.Clear();

        HelpTree.RootNodes.Add(CreateNode("1. Getting Started", "start"));

        var setupNode = CreateNode("2. Setup", "sources");
        setupNode.IsExpanded = true;
        setupNode.Children.Add(CreateNode("2.1 Onboarding and Sources", "sources"));

        var steamNode = CreateNode("2.2 Steam", "steam-connect");
        steamNode.Children.Add(CreateNode("2.2.1 Connect Steam Account", "steam-connect"));
        steamNode.Children.Add(CreateNode("2.2.2 Get Steam API Key", "steam-key"));
        steamNode.IsExpanded = true;
        setupNode.Children.Add(steamNode);

        setupNode.Children.Add(CreateNode("2.3 Connect Epic Account", "epic-connect"));

        var igdbNode = CreateNode("2.4 IGDB", "igdb-create");
        igdbNode.Children.Add(CreateNode("2.4.1 Create Twitch App", "igdb-create"));
        igdbNode.Children.Add(CreateNode("2.4.2 Add IGDB Keys in Mystical", "igdb-add"));
        igdbNode.IsExpanded = true;
        setupNode.Children.Add(igdbNode);

        HelpTree.RootNodes.Add(setupNode);

        var usageNode = CreateNode("3. Using Mystical", "library-usage");
        usageNode.IsExpanded = true;
        usageNode.Children.Add(CreateNode("3.1 Library Workflow", "library-usage"));
        usageNode.Children.Add(CreateNode("3.2 Deals", "deals"));
        usageNode.Children.Add(CreateNode("3.3 Updates", "updates"));
        usageNode.Children.Add(CreateNode("3.4 Stats", "stats"));
        usageNode.Children.Add(CreateNode("3.5 Settings", "settings"));
        HelpTree.RootNodes.Add(usageNode);

        HelpTree.RootNodes.Add(CreateNode("4. Troubleshooting", "troubleshooting"));
    }

    private TreeViewNode CreateNode(string title, string sectionKey = "")
    {
        var node = new TreeViewNode
        {
            Content = new HelpTreeEntry(title, sectionKey)
        };

        if (!string.IsNullOrWhiteSpace(sectionKey))
        {
            _sectionNodes[sectionKey] = node;
            _titleToSectionKey[title] = sectionKey;
        }

        return node;
    }

    private string ResolveSectionKey(object? invokedItem)
    {
        if (invokedItem is TreeViewNode node)
        {
            return ResolveSectionKey(node.Content);
        }

        if (invokedItem is HelpTreeEntry directEntry && !string.IsNullOrWhiteSpace(directEntry.SectionKey))
        {
            return directEntry.SectionKey;
        }

        if (invokedItem is string directTitle && _titleToSectionKey.TryGetValue(directTitle, out var resolved))
        {
            return resolved;
        }

        return string.Empty;
    }

    private static Dictionary<string, HelpDocSection> BuildSections()
    {
        var sections = new[]
        {
            new HelpDocSection
            {
                SectionKey = "start",
                NumberedTitle = "1. Getting Started",
                Summary = "Mystical combines installed and account-owned PC games from supported launchers into one unified library.",
                Links = new[]
                {
                    new HelpDocLink { Label = "Go to Onboarding and Sources", Url = "help:sources" }
                },
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "1.1 First launch",
                        Steps = new[]
                        {
                            "Complete onboarding to choose import sources.",
                            "Open Library and click Refresh to run your first scan.",
                            "Use the platform filter and search box to verify imported entries."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "1.2 Cover art quality",
                        Steps = new[]
                        {
                            "Enable online cover art in Settings -> Library.",
                            "Add IGDB credentials for consistent cover matching.",
                            "Run Refresh after key changes to update existing covers."
                        },
                        Links = new[]
                        {
                            new HelpDocLink { Label = "Go to IGDB Setup", Url = "help:igdb-create" }
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "sources",
                NumberedTitle = "2.1 Onboarding and Sources",
                Summary = "Source toggles control where games are imported from. You can change them anytime.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.1.1 Configure source toggles",
                        Steps = new[]
                        {
                            "Open Settings -> Library.",
                            "Enable or disable Steam, Epic, Xbox/Microsoft Store, and GOG.",
                            "Wait for auto-save confirmation."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.1.2 Rescan library",
                        Steps = new[]
                        {
                            "Go back to Library.",
                            "Click Refresh.",
                            "Review Status to confirm the scan completed."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "steam-connect",
                NumberedTitle = "2.2.1 Connect Steam Account",
                Summary = "Link Steam to import owned titles even when they are not currently installed.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.2.1.1 Link flow",
                        Steps = new[]
                        {
                            "Open Settings -> Accounts.",
                            "Enable Steam account sync.",
                            "Click Connect Steam.",
                            "If needed, paste SteamID64 or profile URL."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.2.1.2 Verify account sync",
                        Steps = new[]
                        {
                            "Confirm Disconnect Steam button appears (connected state).",
                            "Open Library and click Refresh.",
                            "Check for not-installed Steam entries."
                        },
                        Links = new[]
                        {
                            new HelpDocLink { Label = "Go to Steam API Key guide", Url = "help:steam-key" }
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "steam-key",
                NumberedTitle = "2.2.2 Get Steam API Key",
                Summary = "Steam API key is optional, but improves reliability when reading Steam ownership details.",
                Links = new[]
                {
                    new HelpDocLink { Label = "Steam API Key page", Url = "https://steamcommunity.com/dev/apikey" }
                },
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.2.2.1 Generate key",
                        Steps = new[]
                        {
                            "Sign into Steam Community in your browser.",
                            "Open the Steam API key page.",
                            "Register a domain (localhost is acceptable for personal use).",
                            "Copy the generated API key."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.2.2.2 Add key to Mystical",
                        Steps = new[]
                        {
                            "Open Settings -> Accounts.",
                            "Paste the key into Steam API key.",
                            "Wait for auto-save and refresh Library."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "epic-connect",
                NumberedTitle = "2.3 Connect Epic Account",
                Summary = "Epic account linking uses an in-app login popup and Legendary behind the scenes.",
                Links = new[]
                {
                    new HelpDocLink { Label = "Epic account portal", Url = "https://www.epicgames.com/account/personal" }
                },
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.3.1 Link flow",
                        Steps = new[]
                        {
                            "Open Settings -> Accounts.",
                            "Enable Epic account cache sync.",
                            "Click Connect Epic and finish sign-in in the popup.",
                            "Approve the login and confirm connected state."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.3.2 Verify imported owned games",
                        Steps = new[]
                        {
                            "Open Library and click Refresh.",
                            "Confirm Epic titles appear even if not installed.",
                            "Use Disconnect Epic if you need to unlink the account."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "igdb-create",
                NumberedTitle = "2.4.1 Create Twitch App (IGDB)",
                Summary = "IGDB access requires Twitch developer credentials: Client ID and Client Secret.",
                Links = new[]
                {
                    new HelpDocLink { Label = "Twitch Developer Console", Url = "https://dev.twitch.tv/console/apps" },
                    new HelpDocLink { Label = "IGDB API docs", Url = "https://api-docs.igdb.com/" }
                },
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.4.1.1 Create app",
                        Steps = new[]
                        {
                            "Sign in to Twitch Developer Console.",
                            "Create a new application.",
                            "Set redirect URL to http://localhost.",
                            "Copy Client ID and generate Client Secret."
                        },
                        Note = "Mystical uses client-credentials flow; the redirect URL is still required by Twitch app creation."
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.4.1.2 Keep credentials private",
                        Steps = new[]
                        {
                            "Do not publish Client Secret in public repositories.",
                            "Use per-user keys and store them in local app settings."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "igdb-add",
                NumberedTitle = "2.4.2 Add IGDB Keys in Mystical",
                Summary = "Adding IGDB credentials enables more accurate and consistent cover art.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.4.2.1 Add credentials",
                        Steps = new[]
                        {
                            "Open Settings -> Library.",
                            "Paste IGDB Client ID and IGDB Client Secret.",
                            "Wait for auto-save confirmation."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "2.4.2.2 Refresh covers",
                        Steps = new[]
                        {
                            "Run Library Refresh after changing keys.",
                            "Use Import Cover for manual overrides on specific games."
                        },
                        Links = new[]
                        {
                            new HelpDocLink { Label = "Go to Library Workflow", Url = "help:library-usage" }
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "library-usage",
                NumberedTitle = "3.1 Library Workflow",
                Summary = "Library is your primary view for searching, filtering, launching, and organizing games.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.1.1 Browse and filter",
                        Steps = new[]
                        {
                            "Use Search to filter by title/description.",
                            "Use Platform filter and Favorites toggle.",
                            "Switch Grid/List with the view-mode icon buttons."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.1.2 Actions",
                        Steps = new[]
                        {
                            "Click Refresh to rescan launchers and linked accounts.",
                            "Select a game and use Launch/Install.",
                            "Use Favorite and Import Cover for quick curation."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "deals",
                NumberedTitle = "3.2 Deals",
                Summary = "Deals surfaces major discounts and free promotions from Steam, Epic, and GOG feeds.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.2.1 Deal categories",
                        Steps = new[]
                        {
                            "Massive Discounts: deeply discounted games.",
                            "Free Right Now: currently claimable free games.",
                            "Free Soon: upcoming free windows."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.2.2 Open offers",
                        Steps = new[]
                        {
                            "Use Open/Claim to launch the store page in your browser.",
                            "Refresh the tab to reload current offers."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "updates",
                NumberedTitle = "3.3 Updates",
                Summary = "Updates checks installed launcher titles and reports games with pending updates.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.3.1 Check now",
                        Steps = new[]
                        {
                            "Open Updates tab.",
                            "Click Check Now to run update detection.",
                            "Review the Updates Found count and list entries."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.3.2 Read notes",
                        Steps = new[]
                        {
                            "Notes explain platform limitations and missing tooling.",
                            "Steam and Epic provide the most complete local update signals."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "stats",
                NumberedTitle = "3.4 Stats",
                Summary = "Stats gives an overview of totals, total play time, platform breakdown, and recent gaming activity heatmap.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.4.1 Core metrics",
                        Steps = new[]
                        {
                            "Total games, installed, not installed, favorites.",
                            "Total storage size, total play time, and most played title."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.4.2 Activity graph",
                        Steps = new[]
                        {
                            "The heatmap shows last-played activity by day over recent weeks.",
                            "Darker cells indicate more activity on that day.",
                            "Hover cells to inspect exact dates and activity counts."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.4.3 Refresh behavior",
                        Steps = new[]
                        {
                            "Use Refresh to recompute stats from current library data.",
                            "Run Library Refresh first if data looks stale."
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "settings",
                NumberedTitle = "3.5 Settings",
                Summary = "Settings controls theme, scan behavior, account linking, startup, and reset actions.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.5.1 Auto-save",
                        Steps = new[]
                        {
                            "All setting changes are saved automatically after edits.",
                            "Use Reset to revert to defaults and auto-save those values."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "3.5.2 Account controls",
                        Steps = new[]
                        {
                            "Connect and disconnect buttons are state-aware.",
                            "Steam/Epic sync toggles control owned-library imports."
                        },
                        Links = new[]
                        {
                            new HelpDocLink { Label = "Steam Account guide", Url = "help:steam-connect" },
                            new HelpDocLink { Label = "Epic Account guide", Url = "help:epic-connect" }
                        }
                    }
                }
            },
            new HelpDocSection
            {
                SectionKey = "troubleshooting",
                NumberedTitle = "4. Troubleshooting",
                Summary = "Use this checklist if games, covers, or account imports look incomplete.",
                Subsections = new[]
                {
                    new HelpDocSubsection
                    {
                        NumberedTitle = "4.1 Missing games",
                        Steps = new[]
                        {
                            "Confirm source toggles are enabled.",
                            "Verify account connection status in Settings -> Accounts.",
                            "Run Library Refresh."
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "4.2 Wrong or missing covers",
                        Steps = new[]
                        {
                            "Check IGDB credentials in Settings.",
                            "Refresh library after key updates.",
                            "Use Import Cover for manual correction."
                        },
                        Links = new[]
                        {
                            new HelpDocLink { Label = "IGDB setup guide", Url = "help:igdb-create" }
                        }
                    },
                    new HelpDocSubsection
                    {
                        NumberedTitle = "4.3 Cache reset",
                        Steps = new[]
                        {
                            "Use Reset Database for metadata rebuild.",
                            "Use Reset App Data if local cache becomes inconsistent.",
                            "Refresh Library after reset actions complete."
                        }
                    }
                }
            }
        };

        return sections.ToDictionary(section => section.SectionKey, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class HelpTreeEntry
    {
        public HelpTreeEntry(string title, string sectionKey)
        {
            Title = title;
            SectionKey = sectionKey;
        }

        public string Title { get; }

        public string SectionKey { get; }

        public override string ToString()
        {
            return Title;
        }
    }
}
