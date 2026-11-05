using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Vintagestory.GameContent;
using Vintagestory.ServerMods;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using System.Reflection;

namespace OpenInventoryCommand;

enum InventoryType
{
    Main,
    Survival,
    Creative,
    Switch,
    Close
}

public class OpenInventoryCommandModSystem : ModSystem
{
    private ICoreClientAPI capi = null!;

    public override void StartClientSide(ICoreClientAPI api)
    {
        capi = api;

        api.ChatCommands.Create("inventory")
            .WithAlias("inv")
            .RequiresPlayer()

            .BeginSubCommand("main")
                .WithAlias("m")
                .WithDescription("Open the main inventory depending on the current gamemode, or close it if already open")
                .HandleWith(args => OnInventory(args, InventoryType.Main))
            .EndSubCommand()

            .BeginSubCommand("survival")
                .WithAlias("s")
                .WithAlias("1")
                .WithDescription("Open the survival inventory, or close it if already open")
                .HandleWith(args => OnInventory(args, InventoryType.Survival))
            .EndSubCommand()

            .BeginSubCommand("creative")
                .WithAlias("c")
                .WithAlias("2")
                .WithDescription("Open the creative inventory, or close it if already open")
                .HandleWith(args => OnInventory(args, InventoryType.Creative))
            .EndSubCommand()

            .BeginSubCommand("switch")
                .WithDescription("Switch to the other inventory, or open the main inventory if none are open")
                .HandleWith(args => OnInventory(args, InventoryType.Switch))
            .EndSubCommand()

            .BeginSubCommand("close")
                .WithDescription("Close any open inventory")
                .HandleWith(args => OnInventory(args, InventoryType.Close))
            .EndSubCommand()
            ;
    }

    private TextCommandResult OnInventory(TextCommandCallingArgs args, InventoryType newType)
    {
        switch (newType)
        {
            case InventoryType.Creative:
                if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
                    return TextCommandResult.Error("You can only use this command in creative mode.");
                break;
        }

        GuiDialog inventory = capi.Gui.LoadedGuis.First(gui => gui is GuiDialogInventory);

        newType = CollapseInventoryType(inventory, newType);

        if (inventory.IsOpened())
        {
            inventory.TryClose();

            if (MatchesCurrentInventoryType(inventory, newType))
            {
                // if the new inventory is the same as the currently open one, just close it without opening again
                SetMainInventory(inventory, InventoryType.Main);
                return TextCommandResult.Success();
            }
        }

        string composeMethodName = GetComposeMethodName(inventory, newType);

        inventory.TryOpen();
        
        MethodInfo? composeInvDialogInfo =
            typeof(GuiDialogInventory)
                .GetMethod(composeMethodName,
                    BindingFlags.NonPublic | BindingFlags.Instance);
        composeInvDialogInfo?.Invoke(inventory, null);

        SetMainInventory(inventory, newType);

        return TextCommandResult.Success();
    }

    private void SetMainInventory(GuiDialog inventory, InventoryType newType)
    {
        string invDialogName = GetInvDialogName(inventory, newType);

        if (typeof(GuiDialogInventory)
            .GetField(invDialogName,
                BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(inventory)
            is not GuiComposer newComposer)
            return;

        inventory.Composers["maininventory"] = newComposer;
    }

    private bool MatchesCurrentInventoryType(GuiDialog inventory, InventoryType newType)
    {
        switch (newType)
        {
            case InventoryType.Switch: return false; // would always return false either way
            case InventoryType.Close: return true; // causes an early return in SetMainInventory
        }

        string invDialogName = GetInvDialogName(inventory, newType);

        if (typeof(GuiDialogInventory)
            .GetField(invDialogName,
                BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(inventory)
            is not GuiComposer newComposer)
            return false;

        GuiComposer prevComposer = inventory.Composers["maininventory"];
        return newComposer == prevComposer;
    }

    private string GetComposeMethodName(GuiDialog inventory, InventoryType type)
    {
        return type switch
        {
            InventoryType.Survival => "ComposeSurvivalInvDialog",
            InventoryType.Creative => "ComposeCreativeInvDialog",
            _ => "ComposeSurvivalInvDialog"
        };
    }

    private string GetInvDialogName(GuiDialog inventory, InventoryType type)
    {
        return type switch
        {
            InventoryType.Survival => "survivalInvDialog",
            InventoryType.Creative => "creativeInvDialog",
            _ => "survivalInvDialog"
        };
    }

    private InventoryType CollapseInventoryType(GuiDialog inventory, InventoryType type)
    {
        return type switch
        {
            InventoryType.Main => CollapseInventoryType(inventory, InventoryType.Creative),
            InventoryType.Creative => capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? InventoryType.Creative : InventoryType.Survival,
            InventoryType.Switch => inventory.IsOpened()
                ? (MatchesCurrentInventoryType(inventory, InventoryType.Creative) ? InventoryType.Survival : CollapseInventoryType(inventory, InventoryType.Creative))
                : CollapseInventoryType(inventory, InventoryType.Main),
            _ => type
        };
    }
}
