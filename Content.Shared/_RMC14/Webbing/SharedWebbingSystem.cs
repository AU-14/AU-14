﻿using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Inventory;
using Robust.Shared.Containers;
using static Content.Shared._RMC14.Webbing.WebbingTransferComponent;

namespace Content.Shared._RMC14.Webbing;

public abstract class SharedWebbingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedCMInventorySystem _cmInventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WebbingClothingComponent, InteractUsingEvent>(OnWebbingClothingInteractUsing);
        SubscribeLocalEvent<WebbingClothingComponent, GetVerbsEvent<InteractionVerb>>(OnWebbingClothingGetVerbs);
        SubscribeLocalEvent<WebbingClothingComponent, EntInsertedIntoContainerMessage>(OnClothingInserted);
        SubscribeLocalEvent<WebbingClothingComponent, EntRemovedFromContainerMessage>(OnClothingRemoved);
    }

    private void OnWebbingClothingInteractUsing(Entity<WebbingClothingComponent> clothing, ref InteractUsingEvent args)
    {
        // Check if clothing item has webbing
        //  If no, attempt to attach webbing
        //   If successful, return
        //  If yes, check if webbing has holster
        //   If yes, attempt to insert into holster and check if it succeeds
        //    If yes, return
        //    If no, attempt to insert item into webbing (same function as attaching)

        if (_cmInventory.TryHolster(args.User, args.Used, clothing))
        {
            args.Handled = true;
            return;
        }

        if (Attach(clothing, args.Used))
        {
            args.Handled = true;
            return;
        }

        /*
        if (!HasWebbing(args.Used, out var webbing))
        {
            if (Attach(clothing, args.Used))
            {
                args.Handled = true;
                return;
            }
        }

        if (HasComp<CMHolsterComponent>(webbing) &&
            HasComp<CMItemSlotsComponent>(webbing))
        {

        }
        */

        // Check if used item was webbing
        // If yes, attempt to attach webbing
        // If no, check if target item already has webbing
        //  If yes, check if target item has holster
        //   If yes, attempt to holster used item
    }

    private void OnWebbingClothingGetVerbs(Entity<WebbingClothingComponent> clothing, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<XenoComponent>(args.User))
            return;

        if (!HasWebbing((clothing, clothing), out var webComp))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = "Remove webbing",
            Act = () => Detach(clothing, user)
        });

        // Check if webbing holster
        if (!HasComp<CMHolsterComponent>(webComp) ||
            !HasComp<CMItemSlotsComponent>(webComp))
            return;

        if(!TryComp(webComp, out ItemSlotsComponent? itemSlotsComp))
            return;

        // Check if holster has gun
        if (_cmInventory.IsEmpty(itemSlotsComp.Owner))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = "Unholster gun",
            Act = () => _cmInventory.TryUnholster(user, webComp)
        });

    }

    public bool HasWebbing(Entity<WebbingClothingComponent?> clothing, out Entity<WebbingComponent> webbing)
    {
        webbing = default;
        if (!Resolve(clothing, ref clothing.Comp, false))
            return false;

        if (!_container.TryGetContainer(clothing, clothing.Comp.Container, out var container) ||
            container.Count <= 0)
        {
            return false;
        }

        var ent = container.ContainedEntities[0];
        if (!TryComp(ent, out WebbingComponent? webbingComp))
        {
            return false;
        }

        webbing = (ent, webbingComp);
        return true;
    }

    protected virtual void OnClothingInserted(Entity<WebbingClothingComponent> clothing, ref EntInsertedIntoContainerMessage args)
    {
        if (clothing.Comp.Container != args.Container.ID)
            return;

        clothing.Comp.Webbing = args.Entity;
        Dirty(clothing);
        _item.VisualsChanged(clothing);
    }

    protected virtual void OnClothingRemoved(Entity<WebbingClothingComponent> clothing, ref EntRemovedFromContainerMessage args)
    {
        if (clothing.Comp.Container != args.Container.ID)
            return;

        clothing.Comp.Webbing = null;
        Dirty(clothing);
        _item.VisualsChanged(clothing);
    }

    public bool Attach(Entity<WebbingClothingComponent> clothing, EntityUid webbing)
    {
        if (!TryComp(webbing, out WebbingComponent? webbingComp) ||
            HasComp<StorageComponent>(clothing) ||
            !HasComp<StorageComponent>(webbing))
        {
            return false;
        }

        var container = _container.EnsureContainer<ContainerSlot>(clothing, clothing.Comp.Container);
        if (container.Count > 0 || !_container.Insert(webbing, container))
            return false;

        EntityManager.AddComponents(clothing, webbingComp.Components);

        var comp = EnsureComp<WebbingTransferComponent>(webbing);
        comp.Clothing = clothing;
        comp.Transfer = TransferType.ToClothing;
        Dirty(webbing, comp);

        return true;
    }

    private void Detach(Entity<WebbingClothingComponent> clothing, EntityUid user)
    {
        if (TerminatingOrDeleted(clothing) || !clothing.Comp.Running)
            return;

        if (!HasWebbing((clothing, clothing), out var webbing))
            return;

        _container.TryRemoveFromContainer(webbing);
        _hands.TryPickupAnyHand(user, webbing);

        EntityManager.AddComponents(webbing, webbing.Comp.Components);

        var comp = EnsureComp<WebbingTransferComponent>(webbing);
        comp.Clothing = clothing;
        comp.Transfer = TransferType.ToWebbing;
        Dirty(webbing, comp);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<WebbingTransferComponent, WebbingComponent>();
        while (query.MoveNext(out var uid, out var transfer, out var webbing))
        {
            // TODO RMC14 remove this once the bug with transferring on same tick upstream is fixed
            if (transfer.Defer)
            {
                transfer.Defer = false;
                continue;
            }

            RemCompDeferred<WebbingTransferComponent>(uid);

            switch (transfer.Transfer)
            {
                case TransferType.ToClothing:
                {
                    if (!TryComp(uid, out StorageComponent? storage) ||
                        transfer.Clothing is not { } clothing)
                    {
                        continue;
                    }

                    foreach (var stored in storage.Container.ContainedEntities.ToArray())
                    {
                        _storage.Insert(clothing, stored, out _, playSound: false);
                    }

                    break;
                }
                case TransferType.ToWebbing:
                {
                    if (transfer.Clothing is not { } clothing)
                        continue;

                    if (TryComp(clothing, out StorageComponent? storage))
                    {
                        foreach (var stored in storage.Container.ContainedEntities.ToArray())
                        {
                            _storage.Insert(uid, stored, out _, playSound: false);
                        }
                    }

                    foreach (var entry in webbing.Components.Values)
                    {
                        RemComp(clothing, entry.Component.GetType());
                    }

                    break;
                }
            }
        }
    }
}
