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
    Creative
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
                .HandleWith(OnInventoryMain)
            .EndSubCommand()

            .BeginSubCommand("survival")
                .WithAlias("s")
                .WithAlias("1")
                .WithDescription("Open the survival inventory, or close it if already open")
                .HandleWith(OnInventorySurvival)
            .EndSubCommand()

            .BeginSubCommand("creative")
                .WithAlias("c")
                .WithAlias("2")
                .WithDescription("Open the creative inventory, or close it if already open")
                .HandleWith(OnInventoryCreative)
            .EndSubCommand()
            ;
    }

    private TextCommandResult OnInventoryMain(TextCommandCallingArgs args)
    {
        return OnInventoryInternal(args, InventoryType.Main);
    }

    private TextCommandResult OnInventorySurvival(TextCommandCallingArgs args)
    {
        return OnInventoryInternal(args, InventoryType.Survival);
    }

    private TextCommandResult OnInventoryCreative(TextCommandCallingArgs args)
    {
        return OnInventoryInternal(args, InventoryType.Creative);
    }

    private TextCommandResult OnInventoryInternal(TextCommandCallingArgs args, InventoryType newType)
    {
        if (newType == InventoryType.Creative && capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative)
            return TextCommandResult.Error("You can only use this command in creative mode.");

        GuiDialog inventory = capi.Gui.LoadedGuis.First(gui => gui is GuiDialogInventory);
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

        string composeMethodName = GetComposeMethodName(newType);

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
        string invDialogName = GetInvDialogName(newType);

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
        string invDialogName = GetInvDialogName(newType);

        if (typeof(GuiDialogInventory)
            .GetField(invDialogName,
                BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(inventory)
            is not GuiComposer newComposer)
            return false;

        GuiComposer prevComposer = inventory.Composers["maininventory"];
        inventory.Composers["maininventory"] = newComposer;
        return newComposer == prevComposer;
    }

    private string GetComposeMethodName(InventoryType type)
    {
        if (type == InventoryType.Main)
        {
            type = capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? InventoryType.Creative : InventoryType.Survival;
        }

        return type switch
        {
            InventoryType.Survival => "ComposeSurvivalInvDialog",
            InventoryType.Creative => "ComposeCreativeInvDialog",
            _ => "ComposeSurvivalInvDialog"
        };
    }

    private string GetInvDialogName(InventoryType type)
    {
        if (type == InventoryType.Main)
        {
            type = capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? InventoryType.Creative : InventoryType.Survival;
        }

        return type switch
        {
            InventoryType.Survival => "survivalInvDialog",
            InventoryType.Creative => "creativeInvDialog",
            _ => "survivalInvDialog"
        };
    }
}
