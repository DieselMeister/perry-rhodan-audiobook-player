﻿module SettingsPage


    open Xamarin.Forms
    open Fabulous
    open Fabulous.XamarinForms
    open Common
    open Xamarin.Essentials
    open Domain


    let hideLastListendSettingsKey = "Rhodan_HideLastListendWhenOnlyOneAudioBookOnDevice"

    type Language =
        | English
        | German


    type Model = {
        Language: Language
        FirstStart: bool
        DataProtectionStuff: bool

        RewindWhenStartAfterShortPeriodInSec:int
        RewindWhenStartAfterShortPeriodInSecModalModel:NumberPickerModal.Model option

        RewindWhenStartAfterLongPeriodInSec:int
        RewindWhenStartAfterLongPeriodInSecModalModel:NumberPickerModal.Model option

        LongPeriodBeginsAfterInMinutes:int
        LongPeriodBeginsAfterInMinutesModalModel:NumberPickerModal.Model option

        JumpDistance:int
        JumpDistanceModalModel:NumberPickerModal.Model option

        DeveloperModeSwitchCounter:int
        DeveloperMode:bool

        ExternalAudioBookFileUrl: string
        ExternalAudioBookName: string

        ShellRef:ViewRef<Shell>
    }


    type Msg =
        | SetLanguage of Language
        | ShowDataProtectionStuff
        | HideDataProtectionStuff


        | OpenJumpDistancePicker
        | JumpDistanceMsg of NumberPickerModal.Msg
        | CloseJumpDistancePicker
        | SetJumpDistanceValue of int

        | OpenRewindWhenStartAfterShortPeriodInSecPicker
        | RewindWhenStartAfterShortPeriodInSecMsg of NumberPickerModal.Msg
        | CloseRewindWhenStartAfterShortPeriodInSecPicker
        | SetRewindWhenStartAfterShortPeriodInSecValue of int

        | OpenRewindWhenStartAfterLongPeriodInSecPicker
        | RewindWhenStartAfterLongPeriodInSecMsg of NumberPickerModal.Msg
        | CloseRewindWhenStartAfterLongPeriodInSecPicker
        | SetRewindWhenStartAfterLongPeriodInSecValue of int

        | OpenLongPeriodBeginsAfterInMinutesPicker
        | LongPeriodBeginsAfterInMinutesMsg of NumberPickerModal.Msg
        | CloseLongPeriodBeginsAfterInMinutesPicker
        | SetLongPeriodBeginsAfterInMinutesValue of int

        | SetDeveloperModeSwitchCounter of int
        | SetDeveloperMode of bool
        | DeleteDatabase
        | ImportDatabase

        | AddExternalAudioBookZipFile
        | ChangeExternalAudioBookFileUrl of url:string
        | ChangeExternalAudioBookName of name:string

        | OpenFeedbackPage


    let strToOptInt (str:string) =
        let (isInt,value) = System.Int32.TryParse(str)
        if isInt then Some value else None

    let initModel shellref firstStart =
        {
            Language = English
            FirstStart = firstStart
            DataProtectionStuff = false
            RewindWhenStartAfterShortPeriodInSec =
                Services.SystemSettings.getRewindWhenStartAfterShortPeriodInSec()
                |> Async.RunSynchronously
            RewindWhenStartAfterShortPeriodInSecModalModel = None
            RewindWhenStartAfterLongPeriodInSec =
                Services.SystemSettings.getRewindWhenStartAfterLongPeriodInSec()
                |> Async.RunSynchronously
            RewindWhenStartAfterLongPeriodInSecModalModel = None
            LongPeriodBeginsAfterInMinutes =
                Services.SystemSettings.getLongPeriodBeginsAfterInMinutes()
                |> Async.RunSynchronously
            LongPeriodBeginsAfterInMinutesModalModel = None
            JumpDistance =
                (Services.SystemSettings.getJumpDistance()
                |> Async.RunSynchronously) / 1000
            JumpDistanceModalModel = None
            ShellRef = shellref
            DeveloperMode =
                (Services.SystemSettings.getDeveloperMode()
                |> Async.RunSynchronously)
            DeveloperModeSwitchCounter = 0

            ExternalAudioBookFileUrl = ""
            ExternalAudioBookName = ""
        }


    let init shellref firstStart =
        initModel shellref firstStart, Cmd.none, None


    let rec update msg (model:Model) =
        match msg with
        | SetLanguage language ->
            model |> onSetLanguageMsg language
        | ShowDataProtectionStuff ->
            model |> onShowDataProtectionStuffMsg
        | HideDataProtectionStuff ->
            model |> onHideDataProtectionStuffMsg


        | OpenJumpDistancePicker ->
            model |> onOpenJumpDistancePicker
        | JumpDistanceMsg msg ->
            model |> onProcessJumpDistanceMsg msg
        | CloseJumpDistancePicker ->
            model |> onCloseJumpDistancePicker
        | SetJumpDistanceValue jd ->
            model |> onSetJumpDistanceValue jd


        | OpenRewindWhenStartAfterShortPeriodInSecPicker ->
            model |> onOpenRewindWhenStartAfterShortPeriodInSecPicker
        | RewindWhenStartAfterShortPeriodInSecMsg msg ->
            model |> onProcessRewindWhenStartAfterShortPeriodInSecMsg msg
        | CloseRewindWhenStartAfterShortPeriodInSecPicker ->
            model |> onCloseRewindWhenStartAfterShortPeriodInSecPicker
        | SetRewindWhenStartAfterShortPeriodInSecValue sec ->
            model |> onSetRewindWhenStartAfterShortPeriodInSecValue sec

        | OpenRewindWhenStartAfterLongPeriodInSecPicker ->
            model |> onOpenRewindWhenStartAfterLongPeriodInSecPicker
        | RewindWhenStartAfterLongPeriodInSecMsg msg ->
            model |> onProcessRewindWhenStartAfterLongPeriodInSecMsg msg
        | CloseRewindWhenStartAfterLongPeriodInSecPicker ->
            model |> onCloseRewindWhenStartAfterLongPeriodInSecPicker
        | SetRewindWhenStartAfterLongPeriodInSecValue sec ->
            model |> onSetRewindWhenStartAfterLongPeriodInSecValue sec

        | OpenLongPeriodBeginsAfterInMinutesPicker ->
            model |> onOpenLongPeriodBeginsAfterInMinutesPicker
        | LongPeriodBeginsAfterInMinutesMsg msg ->
            model |> onProcessLongPeriodBeginsAfterInMinutesMsg msg
        | CloseLongPeriodBeginsAfterInMinutesPicker ->
            model |> onCloseLongPeriodBeginsAfterInMinutesPicker
        | SetLongPeriodBeginsAfterInMinutesValue min ->
            model |> onSetLongPeriodBeginsAfterInMinutesValue min

        | SetDeveloperMode value ->
            model |> onSetDeveloperMode value
        | SetDeveloperModeSwitchCounter value ->
            model |> onSetDeveloperSwitchCounter value
        | OpenFeedbackPage ->
            // will be handled by the app.fs
            model, Cmd.none, None
        | DeleteDatabase ->
            model, Cmd.none, None
        | ImportDatabase ->
            let cmd =
                fun dispatch ->
                    async {
                        let fileTypes =
                            [
                                (DevicePlatform.Android, ["*/*"] |> List.toSeq )
                            ] |> dict
                        let! result = FilePicker.PickMultipleAsync(PickOptions(PickerTitle="Select Database Files", FileTypes = FilePickerFileType(fileTypes))) |> Async.AwaitTask
                        if result |> isNull |> not then
                            do! Async.Sleep 1000
                            do! Migrations.AudiobooksMissingAfterUpdateAndroid10.importDatabases (result |> Seq.map (fun x -> x.FullPath))
                    }
                    |> Async.StartImmediate
                |> Cmd.ofSub
            model, cmd, None

        | AddExternalAudioBookZipFile ->
            let cmd =
                fun dispatch ->
                    async {
                        if (model.ExternalAudioBookFileUrl = "" || model.ExternalAudioBookName = "") then
                            do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", "Bitte Namen und Url angeben!", "OK!")
                        else

                            let audioBook =
                                {
                                    AudioBook.Empty with
                                        FullName = model.ExternalAudioBookName
                                        EpisodenTitel =
                                            if model.ExternalAudioBookName.Contains ("-") then
                                                model.ExternalAudioBookName.Split([| '-' |]).[1]
                                            else
                                                ""
                                        DownloadUrl = Some model.ExternalAudioBookFileUrl
                                        Id = 16000000 + (System.Random().Next(500000))
                                        Group = "External Audiobooks"

                                }
                            let! res =  Services.DataBase.insertNewAudioBooksInStateFile [| audioBook |]
                            match res with
                            | Error e ->
                                do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", e, "OK!")
                            | Ok _ ->
                                do! Common.Helpers.displayAlert("Externes Audiobook hinzufügen", "Erfolgreich!", "OK!")
                    }
                    |> Async.StartImmediate
                |> Cmd.ofSub
            model, cmd, None
        | ChangeExternalAudioBookFileUrl url ->
            { model with ExternalAudioBookFileUrl = url }, Cmd.none, None
        | ChangeExternalAudioBookName name ->
            { model with ExternalAudioBookName = name }, Cmd.none, None


    and onSetDeveloperSwitchCounter value model  =
        let switchCmd =
            if value > 5 then
                Cmd.ofMsg (SetDeveloperMode (not model.DeveloperMode))
            else
                Cmd.none
        let resetCmd =
            if value = 1 then
                async {
                    do! Async.Sleep 3000
                    return (SetDeveloperModeSwitchCounter 0)
                } |> Cmd.ofAsyncMsg
            else
                Cmd.none

        {model with DeveloperModeSwitchCounter = value},Cmd.batch [ switchCmd; resetCmd ],None

    and onSetDeveloperMode value model =
        Services.SystemSettings.setDeveloperMode value |> Async.RunSynchronously
        {model with DeveloperMode = value;},Cmd.none,None


    and onSetJumpDistanceValue jd model =
        let jdMs = jd * 1000
        Services.SystemSettings.setJumpDistance jdMs |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with JumpDistance = jd; JumpDistanceModalModel = None},Cmd.none,None


    and onOpenJumpDistancePicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectJumpDistance [5..5..60] model.JumpDistance

        let openCmd =
            fun dispatch ->
                let page = NumberPickerModal.view mdl (JumpDistanceMsg >> dispatch)
                Common.ModalBaseHelpers.pushModal dispatch CloseJumpDistancePicker NumberPickerModal.modalTitle model.ShellRef page
            |> Cmd.ofSub

        {model with JumpDistanceModalModel = Some mdl},openCmd,None


    and onCloseJumpDistancePicker model =
        {model with JumpDistanceModalModel = None},Cmd.none,None

    and onProcessJumpDistanceMsg msg model =
        match model.JumpDistanceModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map JumpDistanceMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetJumpDistanceValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none

            let updateCmd =
                fun dispatch ->
                    let page = NumberPickerModal.view mdl (JumpDistanceMsg >> dispatch)
                    Common.ModalBaseHelpers.updateModal dispatch CloseJumpDistancePicker NumberPickerModal.modalTitle model.ShellRef page
                |> Cmd.ofSub

            {model with JumpDistanceModalModel = Some mdl},Cmd.batch [ updateCmd; cmd; exCmd  ],None



    and onSetRewindWhenStartAfterShortPeriodInSecValue sec model =
        Services.SystemSettings.setRewindWhenStartAfterShortPeriodInSec sec |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with RewindWhenStartAfterShortPeriodInSec = sec; RewindWhenStartAfterShortPeriodInSecModalModel = None},Cmd.none,None


    and onOpenRewindWhenStartAfterShortPeriodInSecPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectRewindWhenStartAfterShortPeriodInSec [0..5..60] model.RewindWhenStartAfterShortPeriodInSec
        let openCmd =
            fun dispatch ->
                let page = NumberPickerModal.view mdl (RewindWhenStartAfterShortPeriodInSecMsg >> dispatch)
                Common.ModalBaseHelpers.pushModal dispatch CloseRewindWhenStartAfterShortPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
            |> Cmd.ofSub

        {model with RewindWhenStartAfterShortPeriodInSecModalModel = Some mdl},openCmd,None


    and onCloseRewindWhenStartAfterShortPeriodInSecPicker model =
        {model with RewindWhenStartAfterShortPeriodInSecModalModel = None},Cmd.none,None

    and onProcessRewindWhenStartAfterShortPeriodInSecMsg msg model =
        match model.RewindWhenStartAfterShortPeriodInSecModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map RewindWhenStartAfterShortPeriodInSecMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetRewindWhenStartAfterShortPeriodInSecValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none
            let updateCmd =
                fun dispatch ->
                    let page = NumberPickerModal.view mdl (RewindWhenStartAfterShortPeriodInSecMsg >> dispatch)
                    Common.ModalBaseHelpers.updateModal dispatch CloseRewindWhenStartAfterShortPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
                |> Cmd.ofSub

            {model with RewindWhenStartAfterShortPeriodInSecModalModel = Some mdl},Cmd.batch [ updateCmd;cmd;exCmd],None



    and onSetRewindWhenStartAfterLongPeriodInSecValue sec model =
        Services.SystemSettings.setRewindWhenStartAfterLongPeriodInSec sec |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with RewindWhenStartAfterLongPeriodInSec = sec; RewindWhenStartAfterLongPeriodInSecModalModel = None},Cmd.none,None


    and onOpenRewindWhenStartAfterLongPeriodInSecPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectRewindWhenStartAfterLongPeriodInSec [0..5..120] model.RewindWhenStartAfterLongPeriodInSec
        let openCmd =
            fun dispatch ->
                let page = NumberPickerModal.view mdl (RewindWhenStartAfterLongPeriodInSecMsg >> dispatch)
                Common.ModalBaseHelpers.pushModal dispatch CloseRewindWhenStartAfterLongPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
            |> Cmd.ofSub

        {model with RewindWhenStartAfterLongPeriodInSecModalModel = Some mdl},openCmd,None


    and onCloseRewindWhenStartAfterLongPeriodInSecPicker model =
        {model with RewindWhenStartAfterLongPeriodInSecModalModel = None},Cmd.none,None

    and onProcessRewindWhenStartAfterLongPeriodInSecMsg msg model =
        match model.RewindWhenStartAfterLongPeriodInSecModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map RewindWhenStartAfterLongPeriodInSecMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetRewindWhenStartAfterLongPeriodInSecValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none

            let updateCmd =
                fun dispatch ->
                    let page = NumberPickerModal.view mdl (RewindWhenStartAfterLongPeriodInSecMsg >> dispatch)
                    Common.ModalBaseHelpers.updateModal dispatch CloseRewindWhenStartAfterLongPeriodInSecPicker NumberPickerModal.modalTitle model.ShellRef page
                |> Cmd.ofSub

            {model with RewindWhenStartAfterLongPeriodInSecModalModel = Some mdl},Cmd.batch [ updateCmd;cmd;exCmd],None



    and onSetLongPeriodBeginsAfterInMinutesValue min model =
        Services.SystemSettings.setLongPeriodBeginsAfterInMinutes min |> Async.RunSynchronously
        Common.ModalBaseHelpers.closeCurrentModal model.ShellRef
        {model with LongPeriodBeginsAfterInMinutes = min; LongPeriodBeginsAfterInMinutesModalModel = None},Cmd.none,None


    and onOpenLongPeriodBeginsAfterInMinutesPicker model =
        let (mdl,cmd) = NumberPickerModal.init Translations.current.SelectLongPeriodBeginsAfterInMinutes [10..10..600] model.LongPeriodBeginsAfterInMinutes
        let openCmd =
            fun dispatch ->
                let page = NumberPickerModal.view mdl (LongPeriodBeginsAfterInMinutesMsg >> dispatch)
                Common.ModalBaseHelpers.pushModal dispatch CloseLongPeriodBeginsAfterInMinutesPicker NumberPickerModal.modalTitle model.ShellRef page
            |> Cmd.ofSub

        {model with LongPeriodBeginsAfterInMinutesModalModel = Some mdl},openCmd,None


    and onCloseLongPeriodBeginsAfterInMinutesPicker model =
        {model with LongPeriodBeginsAfterInMinutesModalModel = None},Cmd.none,None

    and onProcessLongPeriodBeginsAfterInMinutesMsg msg model =
        match model.LongPeriodBeginsAfterInMinutesModalModel with
        | None ->
            model,Cmd.none,None
        | Some jdModel ->
            let (mdl,cmd,exCmd) = NumberPickerModal.update msg jdModel
            let cmd = cmd |> Cmd.map LongPeriodBeginsAfterInMinutesMsg
            let exCmd =
                exCmd
                |> Option.map (fun c->
                    match c with
                    | NumberPickerModal.ExternalMsg.TakeValue ->
                        Cmd.ofMsg (SetLongPeriodBeginsAfterInMinutesValue (mdl.Value))
                )
                |> Option.defaultValue Cmd.none

            let updateCmd =
                fun dispatch ->
                    let page = NumberPickerModal.view mdl (LongPeriodBeginsAfterInMinutesMsg >> dispatch)
                    Common.ModalBaseHelpers.updateModal dispatch CloseLongPeriodBeginsAfterInMinutesPicker NumberPickerModal.modalTitle model.ShellRef page
                |> Cmd.ofSub


            {model with LongPeriodBeginsAfterInMinutesModalModel = Some mdl},Cmd.batch [ updateCmd;cmd;exCmd],None




    and onSetLanguageMsg language (model:Model) =
        { model with Language = language }, Cmd.none, None


    and onShowDataProtectionStuffMsg model =
        { model with DataProtectionStuff = true }, Cmd.none, None


    and onHideDataProtectionStuffMsg model =
        { model with DataProtectionStuff = false }, Cmd.none, None






    let settingsEntry label value onTap =

        View.Grid(
            rowdefs=[Auto;Auto;Absolute 1.0],
            horizontalOptions=LayoutOptions.Start,
            children=[
                (Controls.primaryTextColorLabel Common.FontSizeHelper.mediumLabel label)
                    .Row(0)
                    .Margin(Thickness(10.,0.,0.,0.))
                    .HorizontalTextAlignment(TextAlignment.Start)
                (Controls.secondaryTextColorLabel Common.FontSizeHelper.smallLabel value)
                    .Row(1)
                    .Margin(Thickness(30.,0.,0.,0.))
                    .HorizontalTextAlignment(TextAlignment.Start)
                View.BoxView(color=Common.Consts.cardColor)
                    .Row(2)
                    .HorizontalOptions(LayoutOptions.Fill)

            ]
        ).GestureRecognizers([
            View.TapGestureRecognizer(command=onTap)
        ]).HorizontalOptions(LayoutOptions.Fill)



    let view model dispatch =

        dependsOn model (fun _ mdl ->
            View.ContentPage(
                title=Translations.current.SettingsPage,useSafeArea=true,
                backgroundColor = Consts.backgroundColor,
                content = View.Grid(

                    children = [
                        yield View.ScrollView(
                                verticalScrollBarVisibility=ScrollBarVisibility.Always,
                                horizontalOptions=LayoutOptions.FillAndExpand,
                                verticalOptions=LayoutOptions.FillAndExpand,
                                content=
                                    View.Grid(
                                        coldefs=[ Star ],
                                        rowdefs=[ Auto; Auto; Auto; Auto;Auto; Auto; Auto; Auto; Auto; Auto; Auto; Auto; Auto ],
                                        gestureRecognizers= [View.TapGestureRecognizer(command=(fun () -> dispatch (SetDeveloperModeSwitchCounter (model.DeveloperModeSwitchCounter + 1))))],
                                        children=[

                                            (settingsEntry
                                                Translations.current.RewindWhenStartAfterShortPeriodInSec
                                                (sprintf "%i %s" mdl.RewindWhenStartAfterShortPeriodInSec Translations.current.Seconds)
                                                (fun ()->dispatch OpenRewindWhenStartAfterShortPeriodInSecPicker)).Row(0)

                                            (settingsEntry
                                                Translations.current.RewindWhenStartAfterLongPeriodInSec
                                                (sprintf "%i %s" mdl.RewindWhenStartAfterLongPeriodInSec Translations.current.Seconds)
                                                (fun ()->dispatch OpenRewindWhenStartAfterLongPeriodInSecPicker)).Row(1)

                                            (settingsEntry
                                                Translations.current.LongPeriodBeginsAfterInMinutes
                                                (sprintf "%i %s" mdl.LongPeriodBeginsAfterInMinutes Translations.current.Minutes)
                                                (fun ()->dispatch OpenLongPeriodBeginsAfterInMinutesPicker)).Row(2)

                                            (settingsEntry
                                                Translations.current.JumpDistance
                                                (sprintf "%i %s" mdl.JumpDistance Translations.current.Seconds)
                                                (fun () -> dispatch OpenJumpDistancePicker)).Row(3)



                                            View.Button(
                                                text="Sende Feedback oder Supportanfrage",
                                                command=(fun ()-> dispatch OpenFeedbackPage),
                                                margin=Thickness(0.,10.,0.,0.)
                                            ).Row(4)

                                            View.Button(
                                                text=(if mdl.DataProtectionStuff then Translations.current.HideDataProtection else Translations.current.ShowDataProtection),
                                                command=(fun ()-> dispatch (if mdl.DataProtectionStuff then HideDataProtectionStuff else ShowDataProtectionStuff)),
                                                margin=Thickness(0.,10.,0.,0.)
                                            ).Row(5)

                                            View.Button(
                                                text="Import Database",
                                                command=(fun ()-> dispatch ImportDatabase),
                                                margin=Thickness(0.,10.,0.,0.)
                                            ).Row(6)

                                            if mdl.DeveloperMode then
                                                (settingsEntry
                                                    "DeveloperMode"
                                                    "Unter anderem kann man Einträge aus der DB löschen"
                                                    (fun () -> ())).Row(7)

                                                (settingsEntry
                                                    "Datenbank löschen"
                                                    "Hiermit löscht an die ganze Datenbank und initialisiert die Anwendung neu."
                                                    (fun () -> dispatch DeleteDatabase)).Row(8)

                                                View.Entry(text = model.ExternalAudioBookFileUrl
                                                , placeholder = "external audiobook url"
                                                , textColor = Consts.primaryTextColor
                                                , backgroundColor = Consts.backgroundColor
                                                , placeholderColor = Consts.secondaryTextColor
                                                , keyboard=Keyboard.Email
                                                , styleId = "login_username"
                                                , completed = (fun t  -> if t <> model.ExternalAudioBookFileUrl then dispatch (ChangeExternalAudioBookFileUrl t))
                                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.ExternalAudioBookFileUrl<>e.Text then dispatch (ChangeExternalAudioBookFileUrl e.Text)))
                                                ).Row(9)

                                                View.Entry(text = model.ExternalAudioBookName
                                                , placeholder = "external audiobook name"
                                                , textColor = Consts.primaryTextColor
                                                , backgroundColor = Consts.backgroundColor
                                                , placeholderColor = Consts.secondaryTextColor
                                                , keyboard=Keyboard.Email
                                                , styleId = "login_username"
                                                , completed = (fun t  -> if t <> model.ExternalAudioBookName then dispatch (ChangeExternalAudioBookName t))
                                                , created = (fun e -> e.Unfocused.Add(fun args -> if model.ExternalAudioBookName<>e.Text then dispatch (ChangeExternalAudioBookName e.Text)))
                                                ).Row(10)
                                                (settingsEntry
                                                "Externes Hörbuch hinzufügen"
                                                "Hiermit wird ein externe Url für ein Hörbuch hinzugefügt. Debugging Zwecke"
                                                (fun () -> dispatch AddExternalAudioBookZipFile)).Row(11)



                                            if (mdl.DataProtectionStuff) then
                                                let viewSource = UrlWebViewSource(Url="https://www.hardt-solutions.com/PrivacyPolicies/EinsAMedienAudioBookPlayer.html")

                                                View.ScrollView(
                                                    verticalScrollBarVisibility=ScrollBarVisibility.Always,
                                                    horizontalOptions=LayoutOptions.FillAndExpand,
                                                    verticalOptions=LayoutOptions.FillAndExpand,
                                                    height=500.0,
                                                    content=View.WebView(source=viewSource,
                                                        horizontalOptions=LayoutOptions.FillAndExpand,
                                                        verticalOptions=LayoutOptions.FillAndExpand,
                                                        height=500.0
                                                    )
                                                ).Row(9)
                                        ]
                                    )
                            )
                    ]
                )
            )
        )






