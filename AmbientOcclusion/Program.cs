﻿using System;
using System.Diagnostics;

namespace AmbientOcclusion
{
    class Program
    {        
        static void Main(string[] args)
        {
            Application app = new Application();

            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.StackTrace);
            }
        }
    }
}
