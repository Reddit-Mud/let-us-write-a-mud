﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RMUD.Commands
{
    internal class Ban : CommandFactory
    {
        public override void Create(CommandParser Parser)
        {
            Parser.AddCommand(
                KeyWord("BANS"),
                "Display the ban list.")
                .ProceduralRule((match, actor) =>
                {
                    Mud.SendMessage(actor, "~~~ ALL SET BANS ~~~");
                    foreach (var proscription in Mud.ProscriptionList.Proscriptions)
                        Mud.SendMessage(actor, proscription.Glob + " : " + proscription.Reason);
                    return PerformResult.Continue;
                });

            Parser.AddCommand(
                Sequence(
                    RequiredRank(500),
                    KeyWord("BAN"),
                    MustMatch("You must supply an ip mask.", SingleWord("GLOB")),
                    MustMatch("You must supply a reason.", Rest("REASON"))),
                "Ban an ip address.")
                .ProceduralRule((match, actor) =>
                {
                    Mud.ProscriptionList.Ban(match.Arguments["GLOB"].ToString(), match.Arguments["REASON"].ToString());
                    Mud.SendGlobalMessage("^<the0> has banned " + match.Arguments["GLOB"].ToString(), actor);
                    return PerformResult.Continue;
                });

            Parser.AddCommand(
                Sequence(
                    RequiredRank(500),
                    KeyWord("UNBAN"),
                    MustMatch("You must supply an ip mask.", SingleWord("GLOB"))),
                "Remove a ban on an ip address.")
                .ProceduralRule((match, actor) =>
                {
                    Mud.ProscriptionList.RemoveBan(match.Arguments["GLOB"].ToString());
                    Mud.SendGlobalMessage("^<the0> removes the ban on " + match.Arguments["GLOB"].ToString(), actor);
                    return PerformResult.Continue;
                });
        }
    }
}