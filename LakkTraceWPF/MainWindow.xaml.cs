using Npgsql;
using System;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool IsProductValidated { get; set; }

        public bool IsCarrierValidated { get; set; }

        public MainWindow()
        {
            //make first textbox focused
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            InitializeComponent();
        }

        //on Enter jumps to next focus, also triggers datavalidation
        private void TxtBxKeyUpEvent(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
                UIElement keyboardFocus = Keyboard.FocusedElement as UIElement;

                if (keyboardFocus != null)
                {
                    keyboardFocus.MoveFocus(tRequest);
                }
                e.Handled = true;

                if (IsBothValid())
                {
                    //time to call the database checking, because both field validated
                    FormCleaner();
                }
            }
            
            if (Keyboard.FocusedElement == productTxbx && productTxbx.Text.Length > 0) //calls the validator for the field in focus
            {
                ProductValidator();
            }

            if (Keyboard.FocusedElement == carrierTxbx && carrierTxbx.Text.Length > 0)
            {
                CarrierValidator();
                
            }
        }

        //calling the validation regex from the app.config
        public bool RegexValidation(string dataToValidate, string datafieldName)
        {
            string rgx = ConfigurationManager.AppSettings[datafieldName];
            return (Regex.IsMatch(dataToValidate, rgx));
        }

        //compares product with the regex rule
        private void ProductValidator()
        {
            if (RegexValidation(productTxbx.Text, "productTxbx"))
            {
                productLbl.Text = "Termék DataMatrix ellenőrizve!";
                IsProductValidated = true;
            }
            else
            {
                productLbl.Text = "Termék DataMatrix nem megfelelő!";
                IsProductValidated = false;
            }
        }

        //compares carrier with regex rule
        private void CarrierValidator()
        {
            if (RegexValidation(carrierTxbx.Text, "carrierTxbx"))
            {
                carrierLbl.Text = "Keret DataMatrix ellenőrizve!";
                IsCarrierValidated = true;
            }
            else
            {
                carrierLbl.Text = "Keret DataMatrix nem megfelelő!";
                IsCarrierValidated = false;
            }
        }

        //checks if both fields are valid
        private bool IsBothValid()
        {
            if (IsProductValidated == true && IsCarrierValidated == true)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        //cleans the form for seamless data input
        private void FormCleaner()
        {
            productTxbx.Text = "";
            productLbl.Text = "";
            carrierTxbx.Text = "";
            carrierLbl.Text = "";
            productTxbx.Focus();
        }

        //closes the app
        private void MainMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //savebutton for whatever, but the job should be done without clicking
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (IsProductValidated)
                MessageBox.Show("product valid");
            if (IsCarrierValidated)
                MessageBox.Show("carrier valid");
            FormCleaner();
        }
    }
}
