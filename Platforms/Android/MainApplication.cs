﻿using Android.App;
using Android.Runtime;
using Microsoft.Maui;

namespace com.companyname.lacasadelsueloradianteapp;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(nint handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}