using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevExpress.Maui.Core;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

using StarterApp.Common.Helpers;
using StarterApp.Common.Models;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarterApp.CustomControls
{
    public partial class ErrorPopupView : ContentView, INotifyPropertyChanged
    {
        public ErrorPopupView ()
        {
         
        }
        #region Fields

        public static readonly BindableProperty ErrorCodeProperty = BindableProperty.Create(nameof(ErrorCode), typeof(string), typeof(ErrorPopupView), propertyChanged: OnErrorCodeChanged);
        public static readonly BindableProperty ErrorMessageProperty = BindableProperty.Create(nameof(ErrorMessage), typeof(string), typeof(ErrorPopupView), propertyChanged: OnErrorMessageChanged);
        public static readonly BindableProperty ErrorReasonProperty = BindableProperty.Create(nameof(ErrorReason), typeof(string), typeof(ErrorPopupView), propertyChanged: OnErrorReasonChanged);
        public static readonly BindableProperty ErrorTitleProperty = BindableProperty.Create(nameof(ErrorTitle), typeof(string), typeof(ErrorPopupView), propertyChanged: OnErrorTitleChanged);

        public static readonly BindableProperty ErrorTypeProperty = BindableProperty.Create(nameof(ErrorType), typeof(string), typeof(ErrorPopupView), propertyChanged: OnErrorTypeChanged);
        public static readonly BindableProperty HelpLinkProperty = BindableProperty.Create(nameof(HelpLink), typeof(string), typeof(ErrorPopupView), propertyChanged: OnHelpLinkChanged);
        public static readonly BindableProperty ShowErrorPopupProperty = BindableProperty.Create(nameof(ShowErrorPopup), typeof(bool), typeof(ErrorPopupView), propertyChanged: OnShowErrorPopupChanged);
        public static readonly BindableProperty TitleContainerColorProperty = BindableProperty.Create(nameof(TitleContainerColor), typeof(Color), typeof(ErrorPopupView), propertyChanged: TitleContainerColorChanged);
        public static readonly BindableProperty TitleOnContainerColorProperty = BindableProperty.Create(nameof(TitleOnContainerColor), typeof(string), typeof(ErrorPopupView), propertyChanged: TitleOnContainerColorChanged);
        public static readonly BindableProperty WhatThisMeansProperty = BindableProperty.Create(nameof(WhatThisMeans), typeof(string), typeof(ErrorPopupView), propertyChanged: OnWhatThisMeansChanged);
        public static readonly BindableProperty WhatYouCanDoProperty = BindableProperty.Create(nameof(WhatYouCanDo), typeof(string), typeof(ErrorPopupView), propertyChanged: OnWhatYouCanDoChanged);
        public static readonly BindableProperty ShowWhatProperty = BindableProperty.Create(nameof(ShowWhat), typeof(bool), typeof(ErrorPopupView));
        public static readonly BindableProperty ShowErrorMessageProperty = BindableProperty.Create(nameof(ShowErrorMessage), typeof(bool), typeof(ErrorPopupView), propertyChanged: OnShowErrorMessageChanged);

        #endregion Fields

        #region Properties

        public bool ShowErrorMessage { get; set; } = true;

        public bool ShowWhat { get; set; } = true;
        public string ErrorCode
        {
            get => (string)GetValue(ErrorCodeProperty);
            set => SetValue(ErrorCodeProperty, value);
        }

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public bool ErrorMoreExpanded { get; set; } = false;
        public string ErrorReason
        {
            get => (string)GetValue(ErrorReasonProperty);
            set => SetValue(ErrorReasonProperty, value);
        }

        public string ErrorTitle
        {
            get => (string)GetValue(ErrorTitleProperty);
            set => SetValue(ErrorTitleProperty, value);
        }

        public string ErrorType { get; set; }
        public string ExpanderIcon { get; set; } = IconCodesMIR.Expand_more;
        public string HelpLink
        {
            get => (string)GetValue(HelpLinkProperty);
            set => SetValue(HelpLinkProperty, value);
        }

        public bool ShowErrorCode { get; set; } = true;
        public bool ShowErrorPopup
        {
            get => (bool)GetValue(ShowErrorPopupProperty);
            set => SetValue(ShowErrorPopupProperty, value);
        }

        public bool ShowInfo { get; set; } = false;
        public Color TitleContainerColor { get; set; } = Colors.Red;
        public Color TitleOnContainerColor { get; set; } = Colors.White;

        public string WhatThisMeans
        {
            get => (string)GetValue(WhatThisMeansProperty);
            set => SetValue(WhatThisMeansProperty, value);
        }

        public string WhatYouCanDo
        {
            get => (string)GetValue(WhatYouCanDoProperty);
            set => SetValue(WhatYouCanDoProperty, value);
        }

        #endregion Properties

        #region Methods
        [RelayCommand] private void CopyErrorMessageToClipBoard()
        {
            try
            {
                string error = $"Error Code: {ErrorCode}\n" +
                             $"Error Title: {ErrorTitle}\n" +
                             $"Error Message: {ErrorMessage}\n" +
                             $"Error Reason: {ErrorReason}\n" +
                             $"What This Means: {WhatThisMeans}\n" +
                             $"What You Can Do: {WhatYouCanDo}";
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Clipboard.Default.SetTextAsync(error);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
            }
        }
        [RelayCommand] void CloseErrorPopup()
        {
            ShowErrorPopup = false;
        }
        [RelayCommand] public void OpenHelpLink(string url)
        {
            if (url != null)
            {
                Launcher.OpenAsync(new Uri(url));
            }
        }

        private static void OnErrorCodeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ErrorCode = (string)newValue;
            control.OnPropertyChanged(nameof(ErrorCode));
            try
            {
                ErrorDetails ed = ErrorDictionary.GetErrorDetails(control.ErrorCode);
                if (ed != null)
                {
                    control.ErrorTitle = ed.ErrorTitle;
                    control.WhatThisMeans = ed.WhatThisMeans;
                    control.WhatYouCanDo = ed.WhatYouCanDo;
                    control.HelpLink = ed.HelpLink;
                    control.ErrorType = ed.Type;
         
                    switch (control.ErrorType)
                    {
                        case "Info":
                            control.ShowErrorCode = false;
                            control.ShowInfo = true;
                            control.TitleContainerColor = ThemeManager.Theme.Scheme.TertiaryContainer;
                            control.TitleOnContainerColor = ThemeManager.Theme.Scheme.OnTertiaryContainer;

                            break;

                        case "Error":
                            control.ShowErrorCode = true;
                            control.ShowErrorMessage = true;
                            control.ShowInfo = false;
                            control.TitleContainerColor = ThemeManager.Theme.Scheme.Error;                           
                            control.TitleOnContainerColor = ThemeManager.Theme.Scheme.OnError;
                            break;

                        default:
                            control.ShowErrorCode = true;
                            control.ShowErrorMessage = true;
                            control.ShowInfo = false;
                            control.TitleContainerColor = ThemeManager.Theme.Scheme.Error;
                            control.TitleOnContainerColor = ThemeManager.Theme.Scheme.OnError;

                            break;
                    }
                    control.ShowWhat = string.IsNullOrWhiteSpace(control.WhatThisMeans) ? false : true;
                    //control.ShowErrorMessage = string.IsNullOrWhiteSpace(control.ErrorMessage) ? false : true;
                    
                    control.OnPropertyChanged(nameof(ShowWhat));
                    control.OnPropertyChanged(nameof(ShowErrorMessage));
                    control.OnPropertyChanged(nameof(ErrorTitle));
                    control.OnPropertyChanged(nameof(ShowInfo));
                    control.OnPropertyChanged(nameof(WhatThisMeans));
                    control.OnPropertyChanged(nameof(WhatYouCanDo));
                    control.OnPropertyChanged(nameof(HelpLink));
                    control.OnPropertyChanged(nameof(ErrorType));
                    control.OnPropertyChanged(nameof(ErrorReason));
                    control.OnPropertyChanged(nameof(ErrorMessage));
                    control.OnPropertyChanged(nameof(TitleContainerColor));
                    control.OnPropertyChanged(nameof(TitleOnContainerColor));
                    
                }
            }
            catch (Exception ex)
            {
                //ErrorHandler.AddLog(ex.ToString());
                throw new Exception(ex.ToString());
            }
        }
        private static void OnShowErrorMessageChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ShowErrorMessage = (bool)newValue;
            control.OnPropertyChanged(nameof(ShowErrorMessage));
        }
        private static void OnErrorMessageChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ErrorMessage = (string)newValue;
            control.OnPropertyChanged(nameof(ErrorMessage));
        }

        private static void OnErrorReasonChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ErrorReason = (string)newValue;
            control.OnPropertyChanged(nameof(ErrorReason));
        }

        private static void OnErrorTitleChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ErrorTitle = (string)newValue;
            control.OnPropertyChanged(nameof(ErrorTitle));
        }

        private static void OnErrorTypeChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ErrorType = (string)newValue;
            control.OnPropertyChanged(nameof(ErrorType));
            
            switch (control.ErrorType)
            {
                case "Info":
                    control.ShowErrorCode = false;
                    control.ShowInfo = true;
                    control.TitleContainerColor = Colors.Blue;//  ThemeManager.Theme.Scheme.SecondaryContainer;
                    control.TitleOnContainerColor = Colors.Black;//ThemeManager.Theme.Scheme.OnSecondaryContainer;

                    break;

                case "Error":
                    control.ShowErrorCode = true;
                    control.ShowInfo = false;
                    control.ShowErrorMessage = true;
                    control.TitleContainerColor = Colors.Red;// ThemeManager.Theme.Scheme.ErrorContainer;
                    control.TitleOnContainerColor = Colors.White;// ThemeManager.Theme.Scheme.OnErrorContainer;
                    break;

                default:
                    control.ShowErrorCode = true;
                    control.ShowInfo = false;
                    control.ShowErrorMessage = true;
                    control.TitleContainerColor = Colors.Red;// ThemeManager.Theme.Scheme.ErrorContainer;
                    control.TitleOnContainerColor = Colors.Blue;// ThemeManager.Theme.Scheme.OnErrorContainer;
                    break;
            }
            control.OnPropertyChanged(nameof(TitleContainerColor));
            control.OnPropertyChanged(nameof(TitleOnContainerColor));
            control.OnPropertyChanged(nameof(ShowErrorCode));
            control.OnPropertyChanged(nameof(ShowInfo));
            
        }

        private static void OnHelpLinkChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.HelpLink = (string)newValue;
            control.OnPropertyChanged(nameof(HelpLink));
        }

        private static void OnShowErrorPopupChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.ShowErrorPopup = (bool)newValue;
            control.OnPropertyChanged(nameof(ShowErrorPopup));
            //ErrorHandler.AddLog("ErrorPopupView ShowErrorPopup: " + newValue);
        }

        private static void OnWhatThisMeansChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.WhatThisMeans = (string)newValue;
            control.OnPropertyChanged(nameof(WhatThisMeans));
        }

        private static void OnWhatYouCanDoChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.WhatYouCanDo = (string)newValue;
            control.OnPropertyChanged(nameof(WhatYouCanDo));
        }

        private static void TitleContainerColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.TitleContainerColor = (Color)newValue;
            control.OnPropertyChanged(nameof(TitleContainerColor));
        }

        private static void TitleOnContainerColorChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var control = (ErrorPopupView)bindable;
            control.TitleOnContainerColor = (Color)newValue;
            control.OnPropertyChanged(nameof(TitleOnContainerColor));
        }


        

        #endregion Methods
    }
}