﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace RMUD
{
    public partial class MudObject
    {
        /// <summary>
        /// Find the locale of an object. The locale is the top level room that encloses the object. 
        ///  For example, if the object is in an open box, on a table, in a room, the room is the locale.
        ///  Closed containers are considered the top level object, so if an object is in a closed box,
        ///  on a table, in a room, the box is the locale.
        /// </summary>
        /// <param name="Of">The object to find the locale of.</param>
        /// <returns>The locale of the object.</returns>
        public static MudObject FindLocale(MudObject Of)
        {
            if (Of == null || Of.Location == null) return Of;

            //If the object is in a container, check to see if that container is open.
            var container = Of.Location as Container;
            if (container != null)
            {
                if (container.RelativeLocationOf(Of) == RelativeLocations.In)
                {
                    if (Of.Location.GetPropertyOrDefault<bool>("open?", false))
                        return FindLocale(Of.Location);
                    else 
                        return Of.Location;
                }
            }

            return FindLocale(Of.Location);
        }
    }
}
