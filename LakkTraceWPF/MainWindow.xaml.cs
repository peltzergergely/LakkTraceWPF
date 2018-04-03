using Npgsql;
using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Reflection;
using System.Windows.Threading;

namespace LakkTraceWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool IsProductValidated { get; set; }
        public bool IsCarrierValidated { get; set; }
        public bool IsLeaderApprovalNeeded { get; set; }
        public bool IsMsgBoxVisible { get; private set; }
        private string heatsinkID { get; set; }
        private string mainboardID { get; set; }
        private Int32 lacquerLoadCounter;

        DispatcherTimer DigitClockTimer = new DispatcherTimer();

        public MainWindow()
        {
            //make first textbox focused
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            InitializeComponent();
            SettingUpTheParameters();
            VarnishDisplay();
            DailyProduction();
            CheckLacquer();
        }

        private void SettingUpTheParameters()
        {
            //clear the texts
            productLbl.Text = "";
            carrierLbl.Text = "";
            dbresultLbl.Text = "";

            //Digit clock timer
            DigitClockTimer.Tick += new EventHandler(Timer_Click);
            DigitClockTimer.Interval = new TimeSpan(0, 0, 1);
            DigitClockTimer.Start();

            // Message Box hide
            MsgBox.Visibility = Visibility.Hidden;

            //Set leader approval to false by deafult
            IsLeaderApprovalNeeded = false;

            // Default value
            lacquerLoadCounter = 0;
        }

        //Digit clock ticking
        private void Timer_Click(object sender, EventArgs e)
        {
            DateTime d;
            d = DateTime.Now;
            string h = "", m = "", s= "";

            if (d.Hour < 10)
                h = "0" + d.Hour.ToString();
            else
                h = d.Hour.ToString();

            if (d.Minute < 10)
                m = "0" + d.Minute.ToString();
            else
                m = d.Minute.ToString();

            if (d.Second < 10)
                s = "0" + d.Second.ToString();
            else
                s = d.Second.ToString();

            clockLbl.Content = h + ":" + m + ":" + s;

            if (int.Parse(s) % 15 == 0)
                DailyProduction();
        }

        // Cancel the close
        private void onClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBoxResult messageBoxResult = MessageBox.Show("Biztosan be akarod zárni az alkalmazást?", "Kilépés", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (messageBoxResult == MessageBoxResult.Yes)
            {
                e.Cancel = false;
            }
            else
                e.Cancel = true;
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
                    if (IsMsgBoxVisible && !IsLeaderApprovalNeeded)
                    {
                        MsgBoxMessage.Text = "";
                        MsgBox.Visibility = Visibility.Hidden;
                        IsLeaderApprovalNeeded = false;
                        IsMsgBoxVisible = false;
                    }

                    if (HsOrMbOrLa())
                        keyboardFocus.MoveFocus(tRequest);

                }

                e.Handled = true;

                if (IsBothValid())
                {
                    //time to call the database checking, because both field validated
                    SaveBtn_Click(sender, e);
                }

                if (Keyboard.FocusedElement == SaveBtn && !IsBothValid())
                {
                    FormErrorDisplay();
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

        private bool HsOrMbOrLa()
        {
            if (productTxbx.Text.Length == 12 && !IsLeaderApprovalNeeded) //if length is 12 and its a heatsink then get the mainboardID from deltaTecServer
            {
                try
                {
                    SqlConnection cnn = new SqlConnection(ConfigurationManager.ConnectionStrings["deltaTecServer"].ConnectionString);
                    cnn.Open();
                    string SQLQuery;
                    SQLQuery = "Select MainBoard_ID from [dbo].[Main] WHERE Heatsink_ID = '" + productTxbx.Text + "'";
                    SqlCommand SQLcmd = new SqlCommand(SQLQuery, cnn);
                    SQLcmd.ExecuteNonQuery();
                    SqlDataAdapter adpm = new SqlDataAdapter(SQLcmd);
                    DataTable dtm = new DataTable();
                    adpm.Fill(dtm);
                    string mbid = dtm.Rows[0]["MainBoard_ID"].ToString();
                    heatsinkID = productTxbx.Text;
                    mainboardID = mbid;
                    productTxbx.Text = mainboardID;
                    ProductValidator();
                    return true;
                }
                catch (Exception msg)
                {
                    MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                    FormCleanerOnUploadFinished();
                    ErrorSound(3);
                    return false;
                }
            }
            else if (productTxbx.Text.Length == 24 && !IsLeaderApprovalNeeded) //if length is 24 and its a mainboardID then get the heatsingID from deltaTecServer
            {
                try
                {
                    SqlConnection cnn = new SqlConnection(ConfigurationManager.ConnectionStrings["deltaTecServer"].ConnectionString);
                    cnn.Open();
                    string StrQuery1m;
                    StrQuery1m = "Select HeatSink_ID from [dbo].[Main] WHERE MainBoard_ID = '" + productTxbx.Text + "'";
                    SqlCommand objcmdm = new SqlCommand(StrQuery1m, cnn);
                    objcmdm.ExecuteNonQuery();
                    SqlDataAdapter adpm = new SqlDataAdapter(objcmdm);
                    DataTable dtm = new DataTable();
                    adpm.Fill(dtm);
                    string mbid = dtm.Rows[0]["HeatSink_ID"].ToString();
                    heatsinkID = mbid;
                    mainboardID = productTxbx.Text;
                    return true;
                }
                catch (Exception msg)
                {
                    MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                    FormCleanerOnUploadFinished();
                    ErrorSound(3);
                    return false;
                }
            }
            else if (productTxbx.Text.Length == ConfigurationManager.AppSettings["lacquerApproval"].Length && IsLeaderApprovalNeeded) // leader approval input
            {
                string pw = ConfigurationManager.AppSettings["lacquerApproval"];
                if (Regex.IsMatch(productTxbx.Text, pw))
                {
                    MsgBoxHide();
                }
                else
                {
                    InvalidInput("LAKKOT ELLENŐRIZNI KELL, SZÓLJ A MŰSZAKVEZETŐNEK!");
                }
                return false;
            }
            else // error message
            {
                if (IsLeaderApprovalNeeded)
                    InvalidInput("LAKKOT ELLENŐRIZNI KELL, SZÓLJ A MŰSZAKVEZETŐNEK!");
                else
                {

                }
                    InvalidInput("NEM MEGFELELŐ VONALKÓDOT OLVASTÁL BE!");
                return false;
            }
        }

        private void InvalidInput(string msg)
        {
            if (productTxbx.Text.Length != 0)
            {
                ErrorSound(3); // 3 -> number of beeps
                productLbl.Text = productTxbx.Text;
                productLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                productLbl.FontWeight = FontWeights.Normal;
                productTxbx.Text = "";
                dbresultLbl.Text = msg;
                dbresultLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                dbresultLbl.FontWeight = FontWeights.UltraBold;
            }
        }


        //Precheck on deltaTecServer if product test result is OK or not
        private bool PreCheck()
        {
            try
            {
                SqlConnection cnn = new SqlConnection(ConfigurationManager.ConnectionStrings["deltaTecServer"].ConnectionString);
                cnn.Open();
                string StrQuery1m;
                StrQuery1m = "Select Result from [dbo].[HeatSinkData] WHERE Heatsink_ID = '" + heatsinkID + "' AND Operation_ID = '10615' ORDER BY Date DESC";
                SqlCommand objcmdm = new SqlCommand(StrQuery1m, cnn);
                objcmdm.ExecuteNonQuery();
                SqlDataAdapter adpm = new SqlDataAdapter(objcmdm);
                DataTable dtm = new DataTable();
                adpm.Fill(dtm);
                if (dtm.Rows[0]["Result"].ToString() == "OK")
                {
                    return true;
                }
                else return false;
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                FormCleanerOnUploadFinished();
                ErrorSound(3);
                return false;
            }
        }

        //calling the validation regex from the app.config
        public bool RegexValidation(string dataToValidate, string datafieldName)
        {
            string rgx = ConfigurationManager.AppSettings[datafieldName];
            return (Regex.IsMatch(dataToValidate, rgx));
        }

        // Syntax check of the product
        private void ProductValidator()
        {
            this.mainStackPanel.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFDFDFD"));
            dbresultLbl.Text = "";

            if (RegexValidation(productTxbx.Text, "productTxbx"))
            {
                IsProductValidated = true;
                productLbl.Text = "OK";
                productLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                productLbl.FontWeight = FontWeights.UltraBold;
            }
            else
            {
                IsProductValidated = false;
                productLbl.Text = "Nem megfelelő!";
                productLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                productLbl.FontWeight = FontWeights.Normal;
            }
        }

        // Syntax check of the carrier 
        private void CarrierValidator()
        {
            if (RegexValidation(carrierTxbx.Text, "carrierTxbx"))
            {
                IsCarrierValidated = true;
                carrierLbl.Text = "OK";
                carrierLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                carrierLbl.FontWeight = FontWeights.UltraBold;
            }
            else
            {
                IsCarrierValidated = false;
                carrierLbl.Text = "Nem megfelelő!";
                carrierLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                carrierLbl.FontWeight = FontWeights.Normal;
            }
        }

        // Check if both Syntax checked
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

        // Cleans the form for seamless data input
        private void FormCleanerOnUploadFinished()
        {
            productTxbx.Text = "";
            carrierTxbx.Text = "";
            heatsinkID = "";
            mainboardID = "";
            IsProductValidated = false;
            IsCarrierValidated = false;
            productLbl.Text = "";
            productLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF000000"));
            productLbl.FontWeight = FontWeights.Normal;
            carrierLbl.Text = "";
            carrierLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF000000"));
            carrierLbl.FontWeight = FontWeights.Normal;
            productTxbx.Focus();
            this.mainStackPanel.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFDFDFD"));
        }

        //Beeping
        private void ErrorSound(int numberOfBeeps)
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                for (int i = 0; i < numberOfBeeps; i++)
                {
                    Console.Beep(5000, 500);
                }
            }).Start();
        }

        // Turns form red and keeps error message displayed on attempt to send wrong data
        private void FormErrorDisplay()
        {
            ErrorSound(5);

            productLbl.Text =productTxbx.Text;
            productLbl.Foreground = Brushes.White;
            productLbl.FontWeight = FontWeights.Normal;
            productTxbx.Text = "";

            carrierLbl.Text = carrierTxbx.Text;
            carrierLbl.Foreground = Brushes.White;
            carrierLbl.FontWeight = FontWeights.Normal;
            carrierTxbx.Text = "";

            IsProductValidated = false;
            IsCarrierValidated = false;
            productTxbx.Focus();
            dbresultLbl.Foreground = Brushes.White;
            carrierLbl.FontWeight = FontWeights.Normal;
            mainStackPanel.Background = new SolidColorBrush(Colors.Red);
        }

        // Gets product from carrier DM 
        private string GetTableName()
        {
            if (carrierTxbx.Text.Contains("BMW"))
                return "bmw";
            else
                return "volvo";
        }

        // Gets TOP/BOT from carrier DM
        private string GetCarrierSide()
        {
            if (carrierTxbx.Text.Contains("TOP"))
                return "TOP";
            else
                return "BOT";
        }

        // Validates before data upload
        private void DbValidation(string table)
        {
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
            // Making connection
            var conn = new NpgsqlConnection(connstring);
            conn.Open();
            // building query
            var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM " + table + " WHERE prod_dm = :prod_dm", conn);
            cmd.Parameters.Add(new NpgsqlParameter("prod_dm", productTxbx.Text));
            Int32 countProd = Convert.ToInt32(cmd.ExecuteScalar());
            
            if (countProd == 0) // If the product DM is not in the DB then uploads
            {
                DbInsert(table);
            }
            else if (countProd == 1) // if it's already in DB then checks which side should be the next
            {
                var carrFromDb = new NpgsqlCommand("SELECT carr_dm FROM " + table + " WHERE prod_dm = '" + productTxbx.Text + "'", conn);
                string carr = carrFromDb.ExecuteScalar().ToString();

                if (carr.Contains("BOT")) //BOT side is already in DB, only TOP will be accepted
                {
                    if (GetCarrierSide() == "TOP")
                    {
                        DbInsert(table);
                    }
                    else
                    {
                        dbresultLbl.Text = "ENNEK A TERMÉKNEK A BOT OLDALA MÁR LAKKOZVA LETT";
                        FormErrorDisplay();
                        DateIntoErrorMessage(table);
                    }
                }
                else // TOP side is already in DB, only BOT will be accepted
                {
                    if (GetCarrierSide() == "BOT")
                    {
                        DbInsert(table);
                    }
                    else
                    {
                        dbresultLbl.Text = "ENNEK A TERMÉKNEK A TOP OLDALA MÁR LAKKOZVA LETT";
                        FormErrorDisplay();
                        DateIntoErrorMessage(table);
                    }
                }               
            }
            else // By this time product should be finished
            {
                dbresultLbl.Text = "EZ A TERMÉK MINDKÉT OLDALA LAKKOZVA LETT";
                FormErrorDisplay();
                DateIntoErrorMessage(table);
            }
            conn.Close();
        }

        //adding current date to the error string
        private void DateIntoErrorMessage(string table)
        {
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
            // Making connection
            var conn = new NpgsqlConnection(connstring);
            conn.Open();
            // building query
            var cmd = new NpgsqlCommand("SELECT timestamp FROM " + table + " WHERE prod_dm = '"+ productLbl.Text+ "' order by timestamp desc limit 1", conn);
            string date = (cmd.ExecuteScalar()).ToString();
            conn.Close();

            dbresultLbl.Text += " "+ date +"-KOR!";
        }

        // Insert values to database
        private void DbInsert(string table) //DB insert
        {

            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                // Making connection with Npgsql provider
                var conn = new NpgsqlConnection(connstring);
                var UploadMoment = DateTime.Now;
                conn.Open();
                // building SQL query
                var cmd = new NpgsqlCommand("INSERT INTO " + table + " (prod_dm, carr_dm, timestamp, workstation) VALUES(:prod_dm, :carr_dm, :timestamp, :workstation)", conn);
                cmd.Parameters.Add(new NpgsqlParameter("prod_dm", productTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("carr_dm", carrierTxbx.Text));
                cmd.Parameters.Add(new NpgsqlParameter("timestamp", UploadMoment));

                string mchName = Environment.MachineName.ToString();
                if (mchName == "DESKTOP-7L1HPPN")
                    mchName = "old_lakk_pc";

                cmd.Parameters.Add(new NpgsqlParameter("workstation", mchName));
                cmd.ExecuteNonQuery();
                //closing connection ASAP
                conn.Close();
                dbresultLbl.Text = UploadMoment.ToString("HH:mm:ss") + "-kor adatok mentve.";
                dbresultLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF09E409"));
                dbresultLbl.FontWeight = FontWeights.UltraBold;
                FormCleanerOnUploadFinished();
                VarnishDisplay();
                DailyProduction();
                CheckLacquer();
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                FormCleanerOnUploadFinished();
                ErrorSound(3);
            }
        }

        // Shows the number of products uploaded this day
        private void DailyProduction()
        {
            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                // Making connection
                var conn = new NpgsqlConnection(connstring);
                conn.Open();
                //building query
                var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE timestamp > 'today'", conn);
                Int32 countbmw = Convert.ToInt32(cmd.ExecuteScalar());
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE timestamp > 'today'", conn);
                Int32 countvolvo = Convert.ToInt32(cmd.ExecuteScalar());

                string machineName = Environment.MachineName.ToString();
                if (machineName == "DESKTOP-7L1HPPN")
                    machineName = "old_lakk_pc";

                //machineName = "DESKTOP-BVFFOIU";
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE timestamp > 'today' AND workstation = '" + machineName + "'", conn);
                Int32 todayCountBmw = Convert.ToInt32(cmd.ExecuteScalar());
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE timestamp > 'today' AND workstation = '" + machineName + "'", conn);
                Int32 todayCountVolvo = Convert.ToInt32(cmd.ExecuteScalar());

                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE workstation = '" + machineName + "'", conn);
                lacquerLoadCounter = Convert.ToInt32(cmd.ExecuteScalar());
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE workstation = '" + machineName + "'", conn);
                lacquerLoadCounter += Convert.ToInt32(cmd.ExecuteScalar());
                conn.Close();

                BMWsum.Content = countbmw.ToString();
                BMWtoday.Content = todayCountBmw.ToString();

                VOLVOsum.Content = countvolvo.ToString();
                VOLVOtoday.Content = todayCountVolvo.ToString();
            }catch(Exception msg)
            {
                MsgBoxShow("Hiba történt! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                FormCleanerOnUploadFinished();
                ErrorSound(3);
            }
        }

        private void CheckLacquer()
        {
            if (lacquerLoadCounter % 100 == 0)
            {
                MsgBoxShow("Ellenőrizni kell a LAKK mennyiségét, szólj a műszakvezetőnek! Utána folytatódhat a munkafolyamat.", true);
            }
        }

        //show the messagebox
        private void MsgBoxShow(string msg, bool needapproval)
        {
            MsgBoxMessage.Text = msg;
            MsgBoxMessage.FontWeight = FontWeights.SemiBold;
            MsgBox.Visibility = Visibility.Visible;
            IsLeaderApprovalNeeded = needapproval;
            IsMsgBoxVisible = true;
        }

        //hide the messagebox
        private void MsgBoxHide()
        {
            MsgBoxMessage.Text = "";
            MsgBox.Visibility = Visibility.Hidden;
            IsLeaderApprovalNeeded = false;
            IsMsgBoxVisible = false;
            productTxbx.Text = "";
            productLbl.Text = "";
            dbresultLbl.Text = "";
        }

        //hide the messagebox by click
        private void EscBtn_Click(object sender, RoutedEventArgs e)
        {
            MsgBoxHide();
            productTxbx.Focus();
        }

        // Shows the current varnish in a gridView
        private DataSet ds = new DataSet();
        private DataTable dt = new DataTable();

        public void VarnishDisplay()
        {
            try
            {
                // connstring stored in App.config
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                var conn = new NpgsqlConnection(connstring);
                conn.Open();

                string mchName = Environment.MachineName.ToString();
                if (mchName == "DESKTOP-7L1HPPN")
                    mchName = "old_lakk_pc";

                var batch = new NpgsqlCommand("SELECT batch" + " FROM lakk where workstation = '" + mchName + "' order by timestamp desc limit 1", conn);
                var expdateYear = new NpgsqlCommand("SELECT date_part('year',expdate)" + " FROM lakk where workstation = '" + mchName + "' order by timestamp desc limit 1", conn);
                var expdateMonth = new NpgsqlCommand("SELECT date_part('month',expdate)" + " FROM lakk where workstation = '" + mchName + "' order by timestamp desc limit 1", conn);
                var timestampYear = new NpgsqlCommand("SELECT date_part('year',timestamp)" + " FROM lakk where workstation = '" + mchName + "' order by timestamp desc limit 1", conn);
                var timestampMonth = new NpgsqlCommand("SELECT date_part('month',timestamp)" + " FROM lakk where workstation = '" + mchName + "' order by timestamp desc limit 1", conn);

                batchName.Content = batch.ExecuteScalar().ToString();
                batchExpDate.Content = expdateYear.ExecuteScalar().ToString()+"/"+expdateMonth.ExecuteScalar().ToString();
                batchLoadDate.Content = timestampYear.ExecuteScalar().ToString()+"/"+timestampMonth.ExecuteScalar().ToString();
                conn.Close();
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                FormCleanerOnUploadFinished();
                ErrorSound(3);
            }
        }

        //closes the app
        private void MainMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        //savebutton for collecting the final elements.
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (PreCheck())
            {
                if (IsBothValid())
                {
                    DbValidation(GetTableName());
                }                
            }
            else
            {
                dbresultLbl.Text = "A TERMÉK NINCS TESZTELVE! NE LAKKOZD, SZÓLJ A MŰSZAKVEZETŐNEK!";
                FormErrorDisplay();
            }
            
        }

        //open database form
        private void DbBtn_Click(object sender, RoutedEventArgs e)
        {
            DatabaseWindow window = new DatabaseWindow();
            window.Owner = this;
            window.Show();
            productTxbx.Focus();
        }

        //open lacquer load
        private void LakkBtn_Click(object sender, RoutedEventArgs e)
        {
            LacquerLoad window = new LacquerLoad();
            window.Owner = this;
            window.Show();
        }
    }
}
