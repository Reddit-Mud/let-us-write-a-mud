﻿using RMUD;

namespace Space
{
    public class DuctTape : MudObject
    {
        public DuctTape()
        {
            SimpleName("duct tape", "duck", "ducttape", "ducktape");
            SetProperty("weight", Weight.Light);
            Article = "some";
        }
    }   
}