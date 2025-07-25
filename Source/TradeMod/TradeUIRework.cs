﻿using Verse;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using RimWorld.Planet;
using System.Reflection;
using System.Reflection.Emit;
using System;
using System.Linq;
using Verse.Sound;
using System.Collections;

/*
 * TODO list
 *
 */

namespace TradeUI
{
    [StaticConstructorOnStartup]
    static class TradeUIRework
    {
        static TradeUIRework()
        {
            //Harmony.DEBUG = true;
            Harmony harm = new Harmony("rimworld.hobtook.tradeui");
            harm.Patch(AccessTools.Method(typeof(RimWorld.Dialog_Trade), nameof(RimWorld.Dialog_Trade.DoWindowContents), null, null), null, null, new HarmonyMethod(typeof(TradeUIRework), nameof(TradeUIRework.DoWindowContentsTranspiler), null), null);
            harm.Patch(AccessTools.Method(typeof(RimWorld.Dialog_Trade), nameof(RimWorld.Dialog_Trade.FillMainRect), null, null), null, null, new HarmonyMethod(typeof(TradeUIRework), nameof(TradeUIRework.FillMainRectTranspiler), null), null);
            harm.PatchAll();
            //Harmony.DEBUG = false;
        }

        static IEnumerable<CodeInstruction> DoWindowContentsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            MethodInfo info = AccessTools.Method(typeof(TransferableUIUtility), nameof(TransferableUIUtility.DoTransferableSorters), new Type[]
            {
                typeof(TransferableSorterDef),
                typeof(TransferableSorterDef),
                typeof(Action<TransferableSorterDef>),
                typeof(Action<TransferableSorterDef>),
            }, null);
            int startIndex = list.FindIndex((CodeInstruction ins) => CodeInstructionExtensions.Calls(ins, info));
            startIndex++;
            bool foundFirstInstance = false;
            int endIndex = -1;
            for (int i = startIndex; i < list.Count; i++)
            {
                if (list[i].LoadsField(AccessTools.Field(typeof(TradeSession), nameof(TradeSession.giftMode))))
                {
                    if (foundFirstInstance)
                    {
                        endIndex = i - 1;
                        break;
                    }
                    else
                    {
                        foundFirstInstance = true;
                        continue;
                    }
                }
            }
            //list.RemoveRange(startIndex, endIndex - startIndex);
            list.RemoveRange(startIndex, list.Count - startIndex - 1);
            list.InsertRange(startIndex, new CodeInstruction[]
            {
                //new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TradeUIRework), "MyDoWindowRect", new Type[]{typeof(Rect) }, null))
                new CodeInstruction(OpCodes.Ldarg_0, null),
                new CodeInstruction(OpCodes.Ldarga, 1), //Rect inRect
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TradeUIRework), nameof(TradeUIRework.MyDoWindowContents))),
            });

            return list.AsEnumerable();
        }

        static void MyDoWindowContents(Dialog_Trade __instance, ref Rect inRect)
        {
            //Debug.LogError($"It works! Rect {inRect.ToString()}");
            // Calculate space for left/right rects
            const float FOOTER_HEIGHT = 110;
            const float BUTTON_HEIGHT = 55;
            Rect twoColumnRect = new Rect(0f, inRect.yMin + TransferableUIUtility.SortersHeight, inRect.width, inRect.height - FOOTER_HEIGHT - TransferableUIUtility.SortersHeight);
            //Debug.LogError(twoColumnRect.ToString());
            //Debug.LogError($"{twoColumnRect.width},{twoColumnRect.height} {twoColumnRect.xMin}:{twoColumnRect.xMax} {twoColumnRect.yMin}:{twoColumnRect.yMax}");
            // DRAW THE LEFT/RIGHT AREAS
            __instance.FillMainRect(twoColumnRect);

            // Draw footer (replaces original entirely)
            Rect footerSilverRect = new Rect(0f, inRect.height - FOOTER_HEIGHT + 3, inRect.width, FOOTER_HEIGHT - BUTTON_HEIGHT - 13);//FOOTER_HEIGHT - 55);
            if (__instance.cachedCurrencyTradeable != null)
            {
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(0f, footerSilverRect.yMin, inRect.width);
                GUI.color = Color.white;
                DrawCurrencyTradableRow(new Rect(0f, footerSilverRect.yMin + 5, footerSilverRect.width, footerSilverRect.height), __instance.cachedCurrencyTradeable, true);
            }

            // Draw bottom buttons
            Text.Font = GameFont.Small;
            Rect buttonsRect = new Rect(inRect.width / 2f - Dialog_Trade.AcceptButtonSize.x / 2f,
                inRect.height - BUTTON_HEIGHT,
                Dialog_Trade.AcceptButtonSize.x,
                Dialog_Trade.AcceptButtonSize.y);

            // end draw footer (replaces original code entirely)
            // WHAT IS THIS? start
            bool hasTradablesSet = false;
            foreach (Tradeable t in TradeSession.deal.tradeables)
            {
                if (t.ActionToDo == TradeAction.PlayerBuys || t.ActionToDo == TradeAction.PlayerSells)
                {
                    hasTradablesSet = true;
                    break;
                }
            }
            if (!hasTradablesSet)
            {
                DrawGreyButton(new Rect(buttonsRect.x - 10f - Dialog_Trade.OtherBottomButtonSize.x, buttonsRect.y, Dialog_Trade.OtherBottomButtonSize.x, Dialog_Trade.OtherBottomButtonSize.y), "ResetButton".Translate(), true, Color.gray);
                DrawGreyButton(buttonsRect, TradeSession.giftMode ? "OfferGifts".Translate() : "AcceptButton".Translate(), true, Color.gray);
            }
            else
            // WHAT IS THIS? end
            {
                if (Widgets.ButtonText(buttonsRect, TradeSession.giftMode ? ("OfferGifts".Translate() + " (" + FactionGiftUtility.GetGoodwillChange(TradeSession.deal.AllTradeables, TradeSession.trader.Faction).ToStringWithSign() + ")") : "AcceptButton".Translate(), true, true, true))
                {
                    System.Action action = delegate ()
                    {
                        bool flag;
                        if (TradeSession.deal.TryExecute(out flag))
                        {
                            if (flag)
                            {
                                SoundDefOf.ExecuteTrade.PlayOneShotOnCamera(null);
                                Caravan caravan = TradeSession.playerNegotiator.GetCaravan();
                                if (caravan != null)
                                {
                                    caravan.RecacheInventory();
                                }
                                __instance.Close(false);
                                return;
                            }
                            __instance.Close(true);
                        }
                    };
                    if (TradeSession.deal.DoesTraderHaveEnoughSilver())
                    {
                        action();
                    }
                    else
                    {
                        __instance.FlashSilver();
                        SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("ConfirmTraderShortFunds".Translate(), action, false, null, WindowLayer.Dialog));
                    }
                    Event.current.Use();
                }

                if (Widgets.ButtonText(new Rect(buttonsRect.x - 10f - Dialog_Trade.OtherBottomButtonSize.x, buttonsRect.y, Dialog_Trade.OtherBottomButtonSize.x, Dialog_Trade.OtherBottomButtonSize.y), "ResetButton".Translate(), true, true, true))
                {
                    Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                    TradeSession.deal.Reset();
                    __instance.CacheTradeables();
                    __instance.CountToTransferChanged();
                }
            }

            if (Widgets.ButtonText(new Rect(buttonsRect.xMax + 10f, buttonsRect.y, Dialog_Trade.OtherBottomButtonSize.x, Dialog_Trade.OtherBottomButtonSize.y), "CancelButton".Translate(), true, true, true))
            {
                __instance.Close(true);
                Event.current.Use();
            }
            float y = Dialog_Trade.OtherBottomButtonSize.y;
            Rect rect5 = new Rect(inRect.width - y, buttonsRect.y, y, y);
            if (Widgets.ButtonImageWithBG(rect5, Dialog_Trade.ShowSellableItemsIcon, new Vector2?(new Vector2(32f, 32f))))
            {
                Find.WindowStack.Add(new Dialog_SellableItems(TradeSession.trader));
            }
            TooltipHandler.TipRegionByKey(rect5, "CommandShowSellableItemsDesc");
            Faction faction = TradeSession.trader.Faction;
            if (faction != null && !__instance.giftsOnly && !faction.def.permanentEnemy)
            {
                Rect rect6 = new Rect(rect5.x - y - 4f, buttonsRect.y, y, y);
                if (TradeSession.giftMode)
                {
                    if (Widgets.ButtonImageWithBG(rect6, Dialog_Trade.TradeModeIcon, new Vector2?(new Vector2(32f, 32f))))
                    {
                        TradeSession.giftMode = false;
                        TradeSession.deal.Reset();
                        __instance.CacheTradeables();
                        __instance.CountToTransferChanged();
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                    }
                    TooltipHandler.TipRegionByKey(rect6, "TradeModeTip");
                }
                else
                {
                    if (Widgets.ButtonImageWithBG(rect6, Dialog_Trade.GiftModeIcon, new Vector2?(new Vector2(32f, 32f))))
                    {
                        TradeSession.giftMode = true;
                        TradeSession.deal.Reset();
                        __instance.CacheTradeables();
                        __instance.CountToTransferChanged();
                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                    }
                    TooltipHandler.TipRegionByKey(rect6, "GiftModeTip", faction.Name);
                }
            }
            GUI.EndGroup();

        }

        public static void DrawCurrencyTradableRow(Rect rect, Tradeable trad, bool highlight)
        {
            if (highlight)
            {
                Widgets.DrawLightHighlight(rect);
            }

            // [        icon [i] Silver     my amount      < transfer amount        their amount            ]

            Text.Font = GameFont.Small;
            GUI.BeginGroup(rect);

            // Draw transfer amount
            Rect transferRect = new Rect(rect.center.x - (240 / 2), 0f, 240f, rect.height);
            bool flash = Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f && trad.IsCurrency;
            TransferableUIUtility.DoCountAdjustInterface(transferRect, trad, 0, trad.GetMinimumToTransfer(), trad.GetMaximumToTransfer(), flash, null, false);
            //GUI.Label(transferRect, "||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");

            // Draw owned amount
            int ourAmount = trad.CountHeldBy(Transactor.Colony);
            //if (ourAmount != 0)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                float ourAmountWidth = 100f;
                var ourRect = new Rect(rect.center.x - (ourAmountWidth / 2) - 300, 0f, ourAmountWidth, rect.height); ;
                if (Mouse.IsOver(ourRect))
                {
                    Widgets.DrawHighlight(ourRect);
                }
                Rect rect8 = ourRect;
                rect8.xMin += 5f;
                rect8.xMax -= 5f;
                Widgets.Label(rect8, ourAmount.ToStringCached());
                TooltipHandler.TipRegionByKey(ourRect, "ColonyCount");
            }

            // Draw their amount
            int theirAmount = trad.CountHeldBy(Transactor.Trader);
            //if (theirAmount != 0 && trad.IsThing)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                float theirAmountWidth = 100f;
                var theirRect = new Rect(rect.center.x - (theirAmountWidth / 2) + 300, 0f, theirAmountWidth, rect.height);
                if (Mouse.IsOver(theirRect))
                {
                    Widgets.DrawHighlight(theirRect);
                }
                Rect rect3 = theirRect;
                rect3.xMin += 5f;
                rect3.xMax -= 5f;
                Widgets.Label(rect3, theirAmount.ToStringCached());
                TooltipHandler.TipRegionByKey(theirRect, "TraderCount");
            }

            // Draw icon, info, name
            float num = rect.width;
            //Log.Message($" full silver rect = {rect.x}, {rect.y}, {rect.width}, {rect.height}");
            Rect idRect = new Rect(0f, 0, num, rect.height);
            //Log.Message($" silver id rect = {idRect.x}, {idRect.y}, {idRect.width}, {idRect.height}");
            //TransferableUIUtility.DrawTransferableInfo(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);
            MyDrawTransferableInfoSilver(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);
            //GUI.Label(idRect, "||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
            GenUI.ResetLabelAlign();
            GUI.EndGroup();
        }

        static void MyDrawTransferableInfoSilver(Transferable trad, Rect idRect, Color labelColor)
        {
            if (!trad.HasAnyThing && trad.IsThing)
            {
                return;
            }
            if (Mouse.IsOver(idRect))
            {
                Widgets.DrawHighlight(idRect);
            }
            Rect rect = new Rect(0f, (idRect.height - 27) / 2f, 27f, 27f);
            if (trad.IsThing)
            {
                Widgets.ThingIcon(rect, trad.AnyThing, 1f, null);
            }
            else
            {
                trad.DrawIcon(rect);
            }
            if (trad.IsThing)
            {
                Widgets.InfoCardButton(40f, (idRect.height / 2f) - 12f, trad.AnyThing);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect rect2 = new Rect(80f, 0f, idRect.width - 80f, idRect.height);
            Text.WordWrap = false;
            GUI.color = labelColor;
            Widgets.Label(rect2, trad.LabelCap);
            GUI.color = Color.white;
            Text.WordWrap = true;
            if (Mouse.IsOver(idRect))
            {
                Transferable localTrad = trad;
                TooltipHandler.TipRegion(idRect, new TipSignal(delegate ()
                {
                    if (!localTrad.HasAnyThing && localTrad.IsThing)
                    {
                        return "";
                    }
                    string text = localTrad.LabelCap;
                    string tipDescription = localTrad.TipDescription;
                    if (!tipDescription.NullOrEmpty())
                    {
                        text = text + ": " + tipDescription + TransferableUIUtility.ContentSourceDescription(localTrad.AnyThing);
                    }
                    return text;
                }, localTrad.GetHashCode()));
            }
        }

        static void DrawGreyButton(Rect rect, string label, bool drawBackground, Color textColor)
        {
            TextAnchor anchor = Text.Anchor;
            Color originalColor = GUI.color;
            if (drawBackground)
            {
                Texture2D atlas = Widgets.ButtonSubtleAtlas;

                var buttonRect = rect.ContractedBy(1);
                Widgets.DrawAtlas(buttonRect, atlas);
            }
            GUI.color = textColor;
            if (!drawBackground)
            {
                GUI.color = textColor;
                if (Mouse.IsOver(rect))
                {
                    GUI.color = Widgets.MouseoverOptionColor;
                }
            }
            if (drawBackground)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleLeft;
            }
            bool wordWrap = Text.WordWrap;
            if (rect.height < Text.LineHeight * 2f)
            {
                Text.WordWrap = false;
            }
            Widgets.Label(rect, label);
            Text.Anchor = anchor;
            GUI.color = originalColor;
            Text.WordWrap = wordWrap;
        }

        static IEnumerable<CodeInstruction> FillMainRectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> list = new List<CodeInstruction>(instructions);
            // Calculate left/right header size
            // Draw headers
            // Draw colony name
            // Draw negotiator name
            // Draw trader name
            // Draw type of trader
            return list.AsEnumerable();
        }

        [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "FillMainRect")]
        public static class Harmony_DialogTrade_FillMainRect
        {
            static bool Prefix(ref UnityEngine.Rect mainRect, ref List<Tradeable> ___cachedTradeables, ref Dialog_Trade __instance)
            {
                // Draw headers
                float halfWidth = mainRect.width / 2f;
                // Draw left header
                Rect leftHeaderRect = new Rect(0, mainRect.y, halfWidth, 85);
                //Log.Message($"[TradeUI] leftHeaderRect ({leftHeaderRect.x}, {leftHeaderRect.y},{leftHeaderRect.width},{leftHeaderRect.height})");
                // Draw colony name
                GUI.BeginGroup(leftHeaderRect);
                var colonyNameRect = new Rect(0, 0, leftHeaderRect.width, leftHeaderRect.height);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(colonyNameRect, Faction.OfPlayer.Name.Truncate(colonyNameRect.width, null));
                // Draw negotiator name
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(0, 30f, leftHeaderRect.width, leftHeaderRect.height - 30),
                    "NegotiatorTradeDialogInfo".Translate(TradeSession.playerNegotiator.Name.ToStringFull,
                    TradeSession.playerNegotiator.GetStatValue(StatDefOf.TradePriceImprovement,
                    true).ToStringPercent()));

                leftHeaderRect.height -= 30;

                // TODO: fix one pixel missing between left/right horizontal lines (but drawing it all in one go caused scroll bar to render on top of white line)
                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(0f, leftHeaderRect.height - 1, leftHeaderRect.width);
                GUI.color = Color.white;

                GUI.EndGroup();

                // Draw right header
                Rect rightHeaderRect = new Rect(halfWidth, mainRect.y, halfWidth, 85);
                //Log.Message($"[TradeUI] rightHeaderRect ({rightHeaderRect.x}, {rightHeaderRect.y},{rightHeaderRect.width},{rightHeaderRect.height})");
                // Draw trader name
                GUI.BeginGroup(rightHeaderRect);
                var traderNameRect = new Rect(0, 0, rightHeaderRect.width, rightHeaderRect.height);
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.UpperCenter;
                string text = TradeSession.trader.TraderName;
                if (Text.CalcSize(text).x > traderNameRect.width)
                {
                    Text.Font = GameFont.Small;
                    text = text.Truncate(traderNameRect.width, null);
                }
                Widgets.Label(traderNameRect, text);
                // Draw type of trader
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(0, 30f, rightHeaderRect.width, rightHeaderRect.height - 30), TradeSession.trader.TraderKind.LabelCap);

                rightHeaderRect.height -= 30;

                GUI.color = Color.gray;
                Widgets.DrawLineHorizontal(0f, rightHeaderRect.height - 1, rightHeaderRect.width);
                GUI.color = Color.white;

                GUI.EndGroup();

                // Draw vertical divider
                /*GUI.BeginGroup(mainRect);
                Widgets.DrawLineVertical(halfWidth - 1, leftHeaderRect.height, mainRect.height - leftHeaderRect.height);
                GUI.EndGroup();*/

                // Calculate scroll height
                float leftHeight = 6f;
                float rightHeight = 6f;
                foreach (var entry in ___cachedTradeables)
                {
                    if (entry.thingsColony != null && entry.thingsColony.Count > 0)
                        leftHeight += 30f;
                    if (entry.thingsTrader != null && entry.thingsTrader.Count > 0)
                        rightHeight += 30f;
                }

                // Draw left view
                Text.Font = GameFont.Small;
                // Start scroll rect down a bit vertically
                Rect leftScrollRect = new Rect(0, mainRect.y + leftHeaderRect.height, halfWidth, mainRect.height - leftHeaderRect.height);
                Rect leftInsideScrollRect = new Rect(0, 0, (leftScrollRect.width - 16f), leftHeight);
                Widgets.BeginScrollView(leftScrollRect, ref TradeUIParameters.Singleton.scrollPositionLeft, leftInsideScrollRect, true);
                float num = 6f;
                float num2 = TradeUIParameters.Singleton.scrollPositionLeft.y - 30f;
                float num3 = TradeUIParameters.Singleton.scrollPositionLeft.y + leftScrollRect.height;
                int num4 = 0;
                for (int i = 0; i < ___cachedTradeables.Count; i++)
                {
                    // Only draw stuff we have
                    if (___cachedTradeables[i].thingsColony == null || ___cachedTradeables[i].thingsColony.Count == 0)
                        continue;

                    if (num > num2 && num < num3)
                    {
                        Rect rect = new Rect(0, num, leftInsideScrollRect.width, 30f);
                        int countToTransfer = ___cachedTradeables[i].CountToTransfer;
                        //RimWorld.TradeUI.DrawTradeableRow(rect, ___cachedTradeables[i], num4);
                        MyDrawTradableRow(rect, ___cachedTradeables[i], num4, true);
                        if (countToTransfer != ___cachedTradeables[i].CountToTransfer)
                        {
                            __instance.CountToTransferChanged();
                        }
                    }
                    num += 30f;
                    num4++;
                }
                Widgets.EndScrollView();

                // Draw right view
                Rect rightScrollRect = new Rect(halfWidth, mainRect.y + rightHeaderRect.height, halfWidth, mainRect.height - rightHeaderRect.height);
                Rect rightInnerRect = new Rect(0, 0, (rightScrollRect.width - 16f), rightHeight);
                Widgets.BeginScrollView(rightScrollRect, ref TradeUIParameters.Singleton.scrollPositionRight, rightInnerRect, true);
                num = 6f;
                num2 = TradeUIParameters.Singleton.scrollPositionRight.y - 30f;
                num3 = TradeUIParameters.Singleton.scrollPositionRight.y + rightScrollRect.height;
                num4 = 0;
                for (int i = 0; i < ___cachedTradeables.Count; i++)
                {
                    // Only draw stuff they have
                    if (___cachedTradeables[i].thingsTrader == null || ___cachedTradeables[i].thingsTrader.Count == 0)
                        continue;

                    if (num > num2 && num < num3)
                    {
                        Rect rect = new Rect(0, num, rightInnerRect.width, 30f);
                        int countToTransfer = ___cachedTradeables[i].CountToTransfer;
                        MyDrawTradableRow(rect, ___cachedTradeables[i], num4, false);
                        //RimWorld.TradeUI.DrawTradeableRow(rect, ___cachedTradeables[i], num4);
                        if (countToTransfer != ___cachedTradeables[i].CountToTransfer)
                        {
                            __instance.CountToTransferChanged();
                        }

                    }
                    num += 30f;
                    num4++;
                }
                Widgets.EndScrollView();
                return false; // Skip vanilla behavior
            }

            /*static void BeginScrollViewForceDraw(Rect outRect, ref Vector2 scrollPosition, Rect viewRect, bool showScrollbars = true)
            {
                if (Widgets.mouseOverScrollViewStack.Count > 0)
                {
                    Widgets.mouseOverScrollViewStack.Push(Widgets.mouseOverScrollViewStack.Peek() && outRect.Contains(Event.current.mousePosition));
                }
                else
                {
                    Widgets.mouseOverScrollViewStack.Push(outRect.Contains(Event.current.mousePosition));
                }
                if (showScrollbars)
                {
                    scrollPosition = GUI.BeginScrollView(outRect, scrollPosition, viewRect, false, true);
                    return;
                }
                scrollPosition = GUI.BeginScrollView(outRect, scrollPosition, viewRect, GUIStyle.none, GUIStyle.none);
            }*/

            // TODO: change this to override DrawTradableRow in order to have Trade Helper support
            public static void MyDrawTradableRow(Rect mainRect, Tradeable trad, int index, bool isOurs)
            {
                if (Mathf.Abs(index) % 2 == 1)
                {
                    Widgets.DrawLightHighlight(mainRect);
                }

                // Hack to prevent formatting for currency
                if (index < 0)
                    mainRect.width -= 16;

                Text.Font = GameFont.Small;
                GUI.BeginGroup(mainRect);
                float xPosition = mainRect.width;

                // Vanilla draws this right-left for some reason
                // our side, should read this
                // LEFT  ---------- RIGHT
                //Icon, info button, name, animal bond/ridability, owned amount, sell price, amount selling, arrows point right

                // their side should read
                // LEFT --------- RIGHT
                // Icon, info button, name, animal bond/ridability, owned amount, buy price, amount buying, arrows point left

                // TDOO: handle somewhere in the trade what happens when I select (sell 10 steel + buy 5 steel)
                // I think this would be an improvement. Split into two tradeables. This would probably affect a large amount of code (more multiplayer patches possibly to sync the new tradables lsit)

                const float COST_WIDTH = 90f;
                const float TRANSFER_WIDTH = 160f;
                const float OWNED_AMOUNT_WIDTH = 75f;

                if (!trad.TraderWillTrade)
                {
                    // Since no price will be shown, we will occupy more space
                    xPosition -= (TRANSFER_WIDTH + COST_WIDTH);
                    Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH + COST_WIDTH, mainRect.height);
                    // But don't actually consume this space since the price will be "drawn" as empty
                    xPosition += COST_WIDTH;

                    // TODO: fix vanilla bug that an item the trader has will show up as "Trader is not willing to buy this". Instead, everything should read "Trader is not willing to trade this" or "Trader is not willing to sell this."
                    RimWorld.TradeUI.DrawWillNotTradeText(rect5, "TraderWillNotTrade".Translate());
                }
                else if (ModsConfig.IdeologyActive && TransferableUIUtility.TradeIsPlayerSellingToSlavery(trad, TradeSession.trader.Faction) && !new HistoryEvent(HistoryEventDefOf.SoldSlave, TradeSession.playerNegotiator.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo())
                {
                    // Since no price will be shown, will we occupy more space
                    xPosition -= (TRANSFER_WIDTH + COST_WIDTH);
                    Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH + COST_WIDTH, mainRect.height);
                    // But don't actually consume this space since the price will be "drawn" as empty
                    xPosition += COST_WIDTH;

                    RimWorld.TradeUI.DrawWillNotTradeText(rect5, "NegotiatorWillNotTradeSlaves".Translate(TradeSession.playerNegotiator));
                    if (Mouse.IsOver(rect5))
                    {
                        Widgets.DrawHighlight(rect5);
                        TooltipHandler.TipRegion(rect5, "NegotiatorWillNotTradeSlavesTip".Translate(TradeSession.playerNegotiator, TradeSession.playerNegotiator.Ideo.name));
                    }
                }
                else
                {
                    xPosition -= TRANSFER_WIDTH;
                    Rect rect5 = new Rect(xPosition, 0f, TRANSFER_WIDTH, mainRect.height);
                    // Drawing left/right arrows and transfer amount
                    bool flash = Time.time - Dialog_Trade.lastCurrencyFlashTime < 1f && trad.IsCurrency;
                    if (isOurs)
                    {
                        TradeUIParameters.Singleton.isDrawingColonyItems = true;
                    }
                    else
                    {
                        TradeUIParameters.Singleton.isDrawingColonyItems = false;
                    }
                    TransferableUIUtility.DoCountAdjustInterface(rect5, trad, index, trad.GetMinimumToTransfer(), trad.GetMaximumToTransfer(), flash, null, false);
                }

                int ownedAmount = trad.CountHeldBy(isOurs ? Transactor.Colony : Transactor.Trader);
                if ((isOurs && ownedAmount != 0) || (!isOurs && ownedAmount != 0 && trad.IsThing))
                {
                    // draw sell/buy price
                    xPosition -= COST_WIDTH;
                    Rect rect6 = new Rect(xPosition, 0f, COST_WIDTH, mainRect.height);
                    Text.Anchor = TextAnchor.MiddleRight;
                    RimWorld.TradeUI.DrawPrice(rect6, trad, isOurs ? TradeAction.PlayerSells : TradeAction.PlayerBuys);

                    // draw owned amount
                    xPosition -= OWNED_AMOUNT_WIDTH;
                    Rect rect7 = new Rect(xPosition, 0f, OWNED_AMOUNT_WIDTH, mainRect.height);
                    if (Mouse.IsOver(rect7))
                    {
                        Widgets.DrawHighlight(rect7);
                    }
                    Text.Anchor = TextAnchor.MiddleRight;
                    Rect ownedAmountRect = rect7;
                    ownedAmountRect.xMin += 5f;
                    ownedAmountRect.xMax -= 5f;
                    Widgets.Label(ownedAmountRect, ownedAmount.ToStringCached());
                    TooltipHandler.TipRegionByKey(rect7, isOurs ? "ColonyCount" : "TraderCount");
                }
                else
                {
                    xPosition -= (OWNED_AMOUNT_WIDTH + COST_WIDTH);
                }

                // draw animal bond/ridability
                TransferableUIUtility.DoExtraIcons(trad, mainRect, ref xPosition);

                // draw Ideaology something
                if (ModsConfig.IdeologyActive)
                {
                    TransferableUIUtility.DrawCaptiveTradeInfo(trad, TradeSession.trader, mainRect, ref xPosition);
                }

                // draw icon, ID icon, name
                // Calculate rect for icon + info button + name
                Rect idRect = new Rect(0f, 0f, xPosition, mainRect.height);
                TransferableUIUtility.DrawTransferableInfo(trad, idRect, trad.TraderWillTrade ? Color.white : RimWorld.TradeUI.NoTradeColor);

                // Cleanup
                GenUI.ResetLabelAlign();
                GUI.EndGroup();
            }

            [HarmonyPatch(typeof(RimWorld.TransferableUIUtility), "DoCountAdjustInterfaceInternal")]
            static class Harmony_TransferableUIUtility_DoCountAdjustInterfaceInternal
            {
                static bool Prefix(Rect rect, Transferable trad, int index, int min, int max, bool flash, bool readOnly)
                {
                    //Log.Message("[TradeUI] TransferableUIUtility.DoCountAdjustInterfaceInternal prefix");

                    // Skip this behavior if we aren't in a trade UI (transport pod)
                    if (!Find.WindowStack.IsOpen<Dialog_Trade>())
                    {
                        //Log.Message("[TradeUI] Dialog_Trade window is not open. Drawing vanilla UI buttons");
                        return true;
                    }

                    rect = rect.Rounded();

                    const float EDGE_MARGIN = 7f;
                    const float ARROW_MARGIN = 10f;
                    // theirs = 135 wide
                    // [60 arrows][ARROW_MARGIN][60 text box][EDGE_MARGIN]
                    // ours
                    // [60 text box][ARROW_MARGIN][60 arrows][EDGE_MARGIN]
                    // Need a width of 125

                    // Theirs 
                    //[30 button][ARROW_MARGIN][60 text box][ARROW_MARGIN][30 button][EDGE_MARGIN]
                    // I am making this ARROW_MARGIN pixels wider than before

                    Rect miniRect = (!trad.Interactive || readOnly) ? rect : new Rect(rect.xMax - ARROW_MARGIN - EDGE_MARGIN - 120f, rect.center.y - 12.5f, 120f + ARROW_MARGIN, 25f).Rounded();

                    // TODO: WHAT IS THIS? Are these pixel coords correct?
                    /*if (flash)
                    {
                        if (TradeUIParameters.Singleton.isDrawingColonyItems)
                            GUI.DrawTexture(new Rect(rect.x, rect.center.y - 12.5f, 90f, 25f).Rounded(), TransferableUIUtility.FlashTex);
                        else
                            GUI.DrawTexture(miniRect, TransferableUIUtility.FlashTex);
                    }*/

                    TransferableOneWay transferableOneWay = trad as TransferableOneWay;
                    bool flag = transferableOneWay != null && transferableOneWay.HasAnyThing && transferableOneWay.AnyThing is Pawn && transferableOneWay.MaxCount == 1;
                    if (!trad.Interactive || readOnly)
                    {
                        if (flag)
                        {
                            bool flag2 = trad.CountToTransfer != 0;
                            Widgets.Checkbox(rect.position, ref flag2, 24f, true, false, null, null);
                        }
                        else
                        {
                            GUI.color = ((trad.CountToTransfer == 0) ? TransferableUIUtility.ZeroCountColor : Color.white);
                            Text.Anchor = TextAnchor.MiddleCenter;
                            // Make transfer amount always positive?
                            Widgets.Label(miniRect, Mathf.Abs(trad.CountToTransfer).ToStringCached());
                            //Widgets.Label(miniRect, trad.CountToTransfer.ToStringCached());
                            //GUI.Label(miniRect, "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");
                        }
                    }
                    else if (flag)
                    {
                        bool flag3 = trad.CountToTransfer != 0;
                        bool flag4 = flag3;
                        Widgets.Checkbox(miniRect.position, ref flag4, 24f, false, true, null, null);
                        if (flag4 != flag3)
                        {
                            if (flag4)
                            {
                                trad.AdjustTo(trad.GetMaximumToTransfer());
                            }
                            else
                            {
                                trad.AdjustTo(trad.GetMinimumToTransfer());
                            }
                        }
                    }
                    else
                    {
                        Rect textRect = miniRect.ContractedBy(2f);
                        if (!TradeUIParameters.Singleton.isDrawingColonyItems)
                        {
                            // Leave room for arrows
                            textRect.xMin += 60f + ARROW_MARGIN;
                        }
                        textRect.width = 55f;

                        // Draw transfer amount
                        // TODO make this always show positive numbers
                        int countToTransfer = trad.CountToTransfer;
                        string editBuffer = trad.EditBuffer;
                        Widgets.TextFieldNumeric<int>(textRect, ref countToTransfer, ref editBuffer, (float)min, (float)max);
                        trad.AdjustTo(countToTransfer);
                        trad.EditBuffer = editBuffer;
                    }

                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = Color.white;
                    if (trad.Interactive && !flag && !readOnly)
                    {
                        TransferablePositiveCountDirection positiveCountDirection = trad.PositiveCountDirection;
                        int num = (positiveCountDirection == TransferablePositiveCountDirection.Source) ? 1 : -1;
                        int num2 = GenUI.CurrentAdjustmentMultiplier();

                        // Fix VANILLA bug that items with durability cause "<<" and ">>" to appear even when there is only one of them
                        // e.g. I have "Flak Pants (normal) 98%" and they have "Flak Pants (normal)" the game will incorrectly show ">>" even though it stacks our pants in entirely different rows
                        bool canTradeRight = false;
                        bool canTradeLeft = false;
                        if (TradeUIParameters.Singleton.isDrawingColonyItems)
                        {
                            // Can trade right is we can modify trade amount by -1
                            canTradeRight = trad.CanAdjustBy(-num * num2).Accepted;
                            // Can trade left is we can modify trade amount by +1
                            canTradeLeft = trad.CanAdjustBy(num * num2).Accepted;
                        }
                        else
                        {
                            // Can trade right is we can modify trade amount by +1
                            canTradeRight = trad.CanAdjustBy(num * num2).Accepted;
                            // Can trade left is we can modify trade amount by -1
                            canTradeLeft = trad.CanAdjustBy(-num * num2).Accepted;
                        }

                        if (!TradeUIParameters.Singleton.isDrawingColonyItems)
                        {
                            Rect rightArrowRect = new Rect(miniRect.x, rect.y, 30f, rect.height);
                            Rect leftArrowRect = new Rect(rightArrowRect.x + rightArrowRect.width, rect.y, 30f, rect.height);
                            {
                                if (canTradeRight)
                                {
                                    var clickResult = DrawNormalButton(rightArrowRect, "<", true, true, Widgets.NormalOptionColor);
                                    switch (clickResult)
                                    {
                                        case MyDraggableResult.LeftPressed:
                                        case MyDraggableResult.LeftDraggedThenPressed:
                                            trad.AdjustBy(num * num2);
                                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                            break;
                                        case MyDraggableResult.RightPressed:
                                        case MyDraggableResult.RightDraggedThenPressed:
                                            //trad.AdjustBy(num * num2);
                                            trad.AdjustTo(trad.GetMaximumToTransfer());
                                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                            break;
                                    }
                                    /*if (Widgets.ButtonText(rightArrowRect, "<", true, true, true))
                                    {
                                        trad.AdjustBy(num * num2);
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                    }*/
                                }
                                else
                                {
                                    DrawGreyButton(rightArrowRect, "<", true, Color.gray);
                                }

                                if (canTradeLeft)
                                {
                                    var clickResult = DrawNormalButton(leftArrowRect, ">", true, true, Widgets.NormalOptionColor);
                                    switch (clickResult)
                                    {
                                        case MyDraggableResult.LeftPressed:
                                        case MyDraggableResult.LeftDraggedThenPressed:
                                            trad.AdjustBy(-num * num2);
                                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                            break;
                                        case MyDraggableResult.RightPressed:
                                        case MyDraggableResult.RightDraggedThenPressed:
                                            //trad.AdjustBy(num * num2);
                                            trad.AdjustTo(trad.GetMinimumToTransfer());
                                            Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                            break;
                                    }
                                    /*if (Widgets.ButtonText(leftArrowRect, ">", true, true, true))
                                    {
                                        trad.AdjustBy(-num * num2);
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                                    }*/
                                }
                                else
                                {
                                    DrawGreyButton(leftArrowRect, ">", true, Color.gray);
                                }
                            }
                        }

                        if (TradeUIParameters.Singleton.isDrawingColonyItems)// && trad.CanAdjustBy(-num * num2).Accepted)
                        {
                            Rect leftArrowRect = new Rect(miniRect.x + 55f + EDGE_MARGIN + 10, rect.y, 30f, rect.height);
                            Rect rightArrowRect = new Rect(leftArrowRect.xMax, rect.y, 30f, rect.height);

                            if (canTradeLeft)
                            {
                                var clickResult = DrawNormalButton(leftArrowRect, "<", true, true, Widgets.NormalOptionColor);
                                switch (clickResult)
                                {
                                    case MyDraggableResult.LeftPressed:
                                    case MyDraggableResult.LeftDraggedThenPressed:
                                        trad.AdjustBy(num * num2);
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                        break;
                                    case MyDraggableResult.RightPressed:
                                    case MyDraggableResult.RightDraggedThenPressed:
                                        if (positiveCountDirection == TransferablePositiveCountDirection.Destination)
                                            trad.AdjustTo(trad.GetMinimumToTransfer());
                                        else
                                            trad.AdjustTo(trad.GetMaximumToTransfer());
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                        break;
                                }
                                /*if (Widgets.ButtonText(leftArrowRect, "<", true, true, true))
                                {
                                    trad.AdjustBy(num * num2);
                                    Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                }*/
                            }
                            else
                            {
                                DrawGreyButton(leftArrowRect, "<", true, Color.gray);
                            }

                            if (canTradeRight)
                            {
                                var clickResult = DrawNormalButton(rightArrowRect, ">", true, true, Widgets.NormalOptionColor);
                                switch (clickResult)
                                {
                                    case MyDraggableResult.LeftPressed:
                                    case MyDraggableResult.LeftDraggedThenPressed:
                                        trad.AdjustBy(-num * num2);
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                        break;
                                    case MyDraggableResult.RightPressed:
                                    case MyDraggableResult.RightDraggedThenPressed:
                                        //trad.AdjustBy(num * num2);
                                        if (positiveCountDirection == TransferablePositiveCountDirection.Destination)
                                            trad.AdjustTo(trad.GetMaximumToTransfer());
                                        else
                                            trad.AdjustTo(trad.GetMinimumToTransfer());
                                        Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_High, null);
                                        break;
                                }
                                /*if (Widgets.ButtonText(rightArrowRect, ">", true, true, true))
                                {
                                    trad.AdjustBy(-num * num2);
                                    Verse.Sound.SoundStarter.PlayOneShotOnCamera(SoundDefOf.Tick_Low, null);
                                }*/
                            }
                            else
                            {
                                DrawGreyButton(rightArrowRect, ">", true, Color.gray);
                            }
                        }
                    }

                    // Draw arrow texture
                    if (trad.CountToTransfer != 0)
                    {
                        float textBoxCenter = 0f;
                        if (!trad.Interactive || readOnly)
                        {
                            // TODO: shift this a little left/right to accomodate the larger text
                            textBoxCenter = miniRect.center.x;
                            if (trad.CountToTransfer > 0)
                                textBoxCenter -= 15;
                            else
                                textBoxCenter += 15;
                        }
                        else if (TradeUIParameters.Singleton.isDrawingColonyItems)
                        {
                            textBoxCenter = miniRect.xMin + 30f;
                        }
                        else
                        {
                            textBoxCenter = miniRect.xMax - 30f - 2.5f;
                        }
                        Rect position = new Rect(textBoxCenter - (float)(TransferableUIUtility.TradeArrow.width / 2),
                            miniRect.y + miniRect.height / 2f - (float)(TransferableUIUtility.TradeArrow.height / 2),
                            (float)TransferableUIUtility.TradeArrow.width,
                            (float)TransferableUIUtility.TradeArrow.height);
                        TransferablePositiveCountDirection positiveCountDirection2 = trad.PositiveCountDirection;
                        if ((positiveCountDirection2 == TransferablePositiveCountDirection.Source && trad.CountToTransfer > 0) || (positiveCountDirection2 == TransferablePositiveCountDirection.Destination && trad.CountToTransfer < 0))
                        {
                            position.x += position.width;
                            position.width *= -1f;
                        }
                        GUI.DrawTexture(position, TransferableUIUtility.TradeArrow);
                    }
                    //GUI.Label(miniRect, "|||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||");

                    // Skip vanilla behavior
                    return false;
                }
            }

            enum MyDraggableResult
            {
                Idle = 0,
                LeftPressed = 1,
                LeftDragged = 2,
                LeftDraggedThenPressed = 3,
                RightPressed = 4,
                RightDragged = 5,
                RightDraggedThenPressed = 6,
            }

            static MyDraggableResult DrawNormalButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound, Color textColor, bool active = true, bool draggable = true)
            {
                var retVal = MyDraggableResult.Idle;
                TextAnchor anchor = Text.Anchor;
                Color color = GUI.color;
                if (drawBackground)
                {
                    Texture2D atlas = Widgets.ButtonBGAtlas;
                    if (Mouse.IsOver(rect))
                    {
                        atlas = Widgets.ButtonBGAtlasMouseover;
                        if (Input.GetMouseButton(0))
                        {
                            atlas = Widgets.ButtonBGAtlasClick;
                        }
                        else if (Input.GetMouseButton(1))
                        {
                            atlas = Widgets.ButtonBGAtlasClick;
                        }

                        /*if (Input.GetMouseButtonUp(0))
                        {
                            retVal = MyDraggableResult.LeftPressed;
                        }
                        else if (Input.GetMouseButtonUp(1))
                        {
                            retVal = MyDraggableResult.RightPressed;
                        }*/
                    }
                    Widgets.DrawAtlas(rect, atlas);
                }

                if (doMouseoverSound)
                {
                    Verse.Sound.MouseoverSounds.DoRegion(rect);
                }
                if (!drawBackground)
                {
                    GUI.color = textColor;
                    if (Mouse.IsOver(rect))
                    {
                        GUI.color = Widgets.MouseoverOptionColor;
                    }
                }
                if (drawBackground)
                {
                    Text.Anchor = TextAnchor.MiddleCenter;
                }
                else
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                }
                bool wordWrap = Text.WordWrap;
                if (rect.height < Text.LineHeight * 2f)
                {
                    Text.WordWrap = false;
                }
                Widgets.Label(rect, label);
                Text.Anchor = anchor;
                GUI.color = color;
                Text.WordWrap = wordWrap;

                if (active && draggable)
                {
                    // Check for right mouse click first
                    if (Mouse.IsOver(rect) && Input.GetMouseButtonUp(1))
                    {
                        retVal = MyDraggableResult.RightPressed;
                    }
                    else
                    {
                        retVal = ButtonInvisibleDraggable(rect, false);
                        //if (retVal != MyDraggableResult.Idle)
                        //Log.Message("returning value " + retVal.ToString());
                    }

                    //return ButtonInvisibleDraggable(rect, false);
                }
                /*if (!active)
                {
                    return Widgets.DraggableResult.Idle;
                }
                if (!Widgets.ButtonInvisible(rect, false))
                {
                    return Widgets.DraggableResult.Idle;
                }*/

                return retVal;
            }

            static MyDraggableResult ButtonInvisibleDraggable(Rect rect, bool doMouseoverSound = false)
            {
                if (doMouseoverSound)
                {
                    Verse.Sound.MouseoverSounds.DoRegion(rect);
                }
                int controlID = GUIUtility.GetControlID(FocusType.Passive, rect);
                if (Mouse.IsOver(rect))
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        //Log.Message("resetting mouse start pos");
                        Widgets.buttonInvisibleDraggable_activeControl = controlID;
                        Widgets.buttonInvisibleDraggable_mouseStart = Input.mousePosition;
                        Widgets.buttonInvisibleDraggable_dragged = false;
                    }
                    /*else if (Input.GetMouseButtonDown(1))
                    {
                        Widgets.buttonInvisibleDraggable_activeControl = controlID;
                        Widgets.buttonInvisibleDraggable_mouseStart = Input.mousePosition;
                        Widgets.buttonInvisibleDraggable_dragged = false;
                        TradeUIParameters.Singleton.isRightDown = false;
                    }*/
                }

                if (Widgets.buttonInvisibleDraggable_activeControl == controlID)
                {
                    if (Input.GetMouseButtonUp(0))
                    {
                        // On the frame that the button is released
                        Widgets.buttonInvisibleDraggable_activeControl = 0;
                        if (!Mouse.IsOver(rect))
                        {
                            return MyDraggableResult.Idle;
                        }
                        if (!Widgets.buttonInvisibleDraggable_dragged)
                        {
                            // We are over the rect + no drag + released
                            return MyDraggableResult.LeftPressed;
                        }
                        // We are over the rect + dragged + released
                        return MyDraggableResult.LeftDraggedThenPressed;
                    }
                    else
                    {
                        if (!Input.GetMouseButton(0))
                        {
                            // Button not released + not down
                            Widgets.buttonInvisibleDraggable_activeControl = 0;
                            return MyDraggableResult.Idle;
                        }
                        if (!Widgets.buttonInvisibleDraggable_dragged && (Widgets.buttonInvisibleDraggable_mouseStart - Input.mousePosition).sqrMagnitude > Widgets.DragStartDistanceSquared)
                        {
                            // Button not released + button is down + hasn't started dragging + drag distance is met
                            Widgets.buttonInvisibleDraggable_dragged = true;
                            return MyDraggableResult.LeftDragged;
                        }
                    }
                }
                return MyDraggableResult.Idle;
            }
        }

        // I think this is safe to keep as-is
        [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "PostOpen")]
        static class Harmony_DialogTrade_PostOpen
        {
            static void Prefix()
            {
                TradeUIParameters.Singleton.Reset();
            }
        }

        [HarmonyPatch(typeof(RimWorld.Dialog_Trade), "InitialSize", MethodType.Getter)]
        static class Harmony_DialogTrade_InitialSize
        {
            static void Postfix(ref Vector2 __result)
            {
                // Make screen wider (without being bigger than the user's screen)
                __result.x = Mathf.Min(UI.screenWidth, __result.x + 360);
            }
        }

        // TODO: might need to make this a transpiler to support TradeHelper
        [HarmonyPatch]
        static class PatchTradingWindowWidth
        {
            static void Postfix(ref Vector2 __result)
            {
                __result.x = Mathf.Min(UI.screenWidth, __result.x + 360);
            }

            public static bool Prepare()
            {
                //return ModLister.HasActiveModWithName("Multiplayer");
                return LoadedModManager.RunningModsListForReading.Any(
                    m => m.PackageId == "rwmt.Multiplayer".ToLowerInvariant()
                    );
            }

            public static MethodInfo TargetMethod()
            {
                System.Type multiplayerTradeUIType = System.Type.GetType("Multiplayer.Client.TradingWindow, Multiplayer");
                var methodInfo = AccessTools.Property(multiplayerTradeUIType, "InitialSize").GetGetMethod();
                if (methodInfo == null)
                    Log.Error("[TradeUI] failed to find multiplayer method info");
                return methodInfo;
            }
        }
    }
}