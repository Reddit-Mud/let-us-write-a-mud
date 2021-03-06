﻿public class skull : RMUD.MudObject
{
    [RMUD.Persist]
    public int ExamineCount { get; set; }

    public override void Initialize()
    {
        PersistInstance(this);

        Short = "human skull";
        Nouns.Add("human", "skull");

        Perform<RMUD.MudObject, RMUD.MudObject>("describe")
            .Do((viewer, thing) =>
            {
                ExamineCount += 1;
                SendMessage(viewer, string.Format("How many times? {0} times.", ExamineCount));
                return RMUD.PerformResult.Continue;
            });
    }

}