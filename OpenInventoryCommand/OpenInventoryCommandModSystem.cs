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
                .WithDescription("Open the main inventory depending on the current gamemode")
                .HandleWith(OnInventoryMain)
            .EndSubCommand()

            .BeginSubCommand("survival")
                .WithAlias("s")
                .WithAlias("1")
                .WithDescription("Open the survival inventory")
                .HandleWith(OnInventorySurvival)
            .EndSubCommand()

            .BeginSubCommand("creative")
                .WithAlias("c")
                .WithAlias("2")
                .WithDescription("Open the creative inventory")
                .HandleWith(OnInventoryCreative)
            .EndSubCommand()
            ;
    }

    private TextCommandResult OnInventoryMain(TextCommandCallingArgs args)
    {
        if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) return OnInventoryCreative(args);
        else return OnInventorySurvival(args);
    }

    private TextCommandResult OnInventorySurvival(TextCommandCallingArgs args)
    {
        GuiDialog inventory = capi.Gui.LoadedGuis.First(gui => gui is GuiDialogInventory);
        if (inventory.IsOpened())
        {
            inventory.TryClose();

            SetMainInventory(inventory, InventoryType.Main);
        }
        else
        {
            inventory.TryOpen();
            MethodInfo? composeSurvivalInvDialogInfo =
                typeof(GuiDialogInventory)
                    .GetMethod("ComposeSurvivalInvDialog",
                        BindingFlags.NonPublic | BindingFlags.Instance);
            composeSurvivalInvDialogInfo?.Invoke(inventory, null);

            SetMainInventory(inventory, InventoryType.Survival);
        }

        return TextCommandResult.Success();
    }

    private TextCommandResult OnInventoryCreative(TextCommandCallingArgs args)
    {
        if (capi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Creative) return TextCommandResult.Error("You can only use this command in creative mode.");

        GuiDialog inventory = capi.Gui.LoadedGuis.First(gui => gui is GuiDialogInventory);
        if (inventory.IsOpened())
        {
            inventory.TryClose();

            SetMainInventory(inventory, InventoryType.Main);
        }
        else
        {
            inventory.TryOpen();
            MethodInfo? composeCreativeInvDialogInfo =
                typeof(GuiDialogInventory)
                    .GetMethod("ComposeCreativeInvDialog",
                        BindingFlags.NonPublic | BindingFlags.Instance);
            composeCreativeInvDialogInfo?.Invoke(inventory, null);

            SetMainInventory(inventory, InventoryType.Creative);
        }

        return TextCommandResult.Success();
    }

    private void SetMainInventory(GuiDialog inventory, InventoryType type)
    {
        if (type == InventoryType.Main)
        {
            type = capi.World.Player.WorldData.CurrentGameMode == EnumGameMode.Creative ? InventoryType.Creative : InventoryType.Survival;
        }

        string inventoryFieldName = type switch
        {
            InventoryType.Survival => "survivalInvDialog",
            InventoryType.Creative => "creativeInvDialog",
            _ => "survivalInvDialog"
        };

        FieldInfo? survivalInvDialogInfo =
            typeof(GuiDialogInventory).GetField(inventoryFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
        if (survivalInvDialogInfo != null)
            inventory.Composers["maininventory"] =
                (GuiComposer)survivalInvDialogInfo.GetValue(inventory)!;
    }
}
