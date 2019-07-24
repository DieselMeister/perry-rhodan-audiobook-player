﻿// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace PerryRhodan.AudiobookPlayer.iOS

open System
open UIKit
open Foundation
open Xamarin.Forms
open Xamarin.Forms.Platform.iOS


//open Plugin.DownloadManager

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit FormsApplicationDelegate ()

    override this.FinishedLaunching (app, options) =
        Forms.Init()
        let appcore = new PerryRhodan.AudiobookPlayer.App()
        this.LoadApplication (appcore)
        base.FinishedLaunching(app, options)

    override this.HandleEventsForBackgroundUrl (application:UIApplication,sessionIdentifier:string,completionHandler:Action) =
        ()

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main(args, null, "AppDelegate")
        0

