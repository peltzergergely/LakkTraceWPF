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
using System.Collections.Generic;
using System.Windows.Controls;

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
        public int IsExpandOpen { get; private set; } = 0;
        private string Interlock { get; set; } = ConfigurationManager.AppSettings["DtInterlock"];

        private Int32 lacquerLoadCounter;

        DispatcherTimer DigitClockTimer = new DispatcherTimer();
        DispatcherTimer ContinuousErrorSound = new DispatcherTimer();

        public MainWindow()
        {
            //make first textbox focused
            Loaded += (sender, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            InitializeComponent();
            SettingUpTheParameters();
            VarnishDisplay();
            DailyProduction();
            UpdateLastShiftStatistic();
            CheckLacquer();
            RefreshSemiFinishedProducts();
        }

        private void RefreshSemiFinishedProducts()
        {
            try
            {
                string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;

                using (var conn = new NpgsqlConnection(connstring))
                {
                    conn.Open();

                    DataSet DS = new DataSet();
                    DataTable DT = new DataTable();

                    string machineName = Environment.MachineName.ToString();
                    if (machineName == "DESKTOP-7L1HPPN")
                        machineName = "old_lakk_pc";

                    string query = @"(SELECT bmw.prod_dm,bmw.timestamp FROM
                                    (
	                                    SELECT COUNT(*) AS db,prod_dm FROM bmw GROUP BY prod_dm
                                    ) AS subq
                                    JOIN bmw ON bmw.prod_dm = subq.prod_dm
                                    WHERE db = 1 AND workstation = '"+ machineName + @"' AND bmw.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))

                                    UNION

                                    (SELECT volvo.prod_dm,volvo.timestamp FROM
                                    (
	                                    SELECT count(*) AS db,prod_dm FROM volvo GROUP BY prod_dm
                                    ) AS subq
                                    JOIN volvo ON volvo.prod_dm = subq.prod_dm
                                    WHERE db = 1 AND workstation = '" + machineName + @"' AND volvo.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))

                                    ORDER BY timestamp LIMIT 70";

                    var DA = new NpgsqlDataAdapter(query, conn);
                    DS.Reset();
                    DA.Fill(DS);
                    DT = DS.Tables[0];

                    semiFinishedProducts.Children.Clear();

                    for (int i = 0; i < DT.Rows.Count; i++)
                    {
                        var exp = new Expander
                        {
                            Header = DT.Rows[i][0].ToString(),
                            BorderThickness = new Thickness(0),
                            Width = 220
                        };
                        exp.Expanded += GetDatasIntoExpand;

                        DateTime timestamp = Convert.ToDateTime(DT.Rows[i][1]);

                        if (DateTime.Now.Subtract(timestamp).TotalMinutes > 40)
                        {
                            exp.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#EA0000"));
                        }
                        else if (DateTime.Now.Subtract(timestamp).TotalMinutes > 25)
                        {
                            exp.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#D1B02E"));
                        }
                        else
                        {
                            exp.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#489324"));
                        }

                        exp.Collapsed += decreaseIsExpandOpen;

                        semiFinishedProducts.Children.Add(exp);
                    }

                    string Countquery = @"select count(*) from
                                        ((SELECT bmw.prod_dm,bmw.timestamp FROM
                                        (
	                                        SELECT COUNT(*) AS db,prod_dm FROM bmw GROUP BY prod_dm
                                        ) AS subq
                                        JOIN bmw ON bmw.prod_dm = subq.prod_dm
                                        WHERE db = 1 AND workstation = '" + machineName + @"' AND bmw.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))

                                        UNION

                                        (SELECT volvo.prod_dm,volvo.timestamp FROM
                                        (
	                                        SELECT count(*) AS db,prod_dm FROM volvo GROUP BY prod_dm
                                        ) AS subq
                                        JOIN volvo ON volvo.prod_dm = subq.prod_dm
                                        WHERE db = 1 AND workstation = '" + machineName + @"' AND volvo.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))) as Q";
                   semiFinishedCount.Text = new NpgsqlCommand(Countquery, conn).ExecuteScalar().ToString();

                }
            }
            catch(Exception msg)
            {
                MessageBox.Show(msg.ToString());
            }

        }

        private void decreaseIsExpandOpen(object sender, RoutedEventArgs e)
        {
            IsExpandOpen--;
        }

        private void GetDatasIntoExpand(object sender, RoutedEventArgs e)
        {
            IsExpandOpen++;
            string prodDm = (sender as Expander).Header.ToString();
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;

            try
            {
                using (var conn = new NpgsqlConnection(connstring))
                {
                    conn.Open();
                    DataSet DS = new DataSet();
                    DataTable DT = new DataTable();

                    string query = @"(SELECT bmw.id,bmw.prod_dm,subq.db,bmw.timestamp,bmw.workstation,bmw.carr_dm FROM
                                (
	                                SELECT COUNT(*) AS db,prod_dm FROM bmw GROUP BY prod_dm
                                ) AS subq
                                JOIN bmw ON bmw.prod_dm = subq.prod_dm
                                WHERE db = 1 AND bmw.prod_dm = '" + prodDm + @"'
                                )

                                UNION

                                (SELECT volvo.id,volvo.prod_dm,subq.db,volvo.timestamp,volvo.workstation,volvo.carr_dm FROM
                                (
	                                SELECT COUNT(*) AS db,prod_dm FROM volvo GROUP BY prod_dm
                                ) AS subq
                                JOIN volvo ON volvo.prod_dm = subq.prod_dm
                                WHERE db = 1 AND volvo.prod_dm = '" + prodDm + @"')
                                ";

                    var DA = new NpgsqlDataAdapter(query, conn);
                    DS.Reset();
                    DA.Fill(DS);
                    DT = DS.Tables[0];

                    string text = "ID: " + DT.Rows[0][0] + Environment.NewLine + "Idő: " + DT.Rows[0][3] + Environment.NewLine + "Munkaállomás: " + DT.Rows[0][4] + Environment.NewLine + "Hiányos oldal: ";

                    if (DT.Rows[0][5].ToString().Contains("BOT"))
                    {
                        text += "TOP";
                    }
                    else if (DT.Rows[0][5].ToString().Contains("TOP"))
                    {
                        text += "BOT";
                    }
                    else
                    {
                        text += "n/a";
                    }

                    if (DT.Rows[0][5].ToString().Contains("BMW"))
                    {
                        text += Environment.NewLine + "Termék: BMW";
                    }
                    else if (DT.Rows[0][5].ToString().Contains("VOLVO"))
                    {
                        text += Environment.NewLine + "Termék: VOLVO";
                    }
                    else
                    {
                        text += Environment.NewLine + "Termék: n/a";
                    }

                    var panel = new StackPanel();
                    panel.Name = prodDm;
                    var textfield = new TextBlock();
                    textfield.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFDFDFD"));
                    textfield.Text = text;
                    panel.Children.Add(textfield);

                    var okBtn = new Button();
                    okBtn.Focusable = false;
                    okBtn.FontWeight = FontWeights.ExtraBold;
                    okBtn.Click += semiFinishedProductsResult;
                    okBtn.Background = Brushes.LimeGreen;
                    okBtn.Foreground = Brushes.White;
                    okBtn.Content = "Lakkozásra került";

                    var nokBtn = new Button();
                    nokBtn.Focusable = false;
                    nokBtn.FontWeight = FontWeights.ExtraBold;
                    nokBtn.Click += semiFinishedProductsResult;
                    nokBtn.Background = Brushes.Crimson;
                    nokBtn.Foreground = Brushes.White;
                    nokBtn.Content = "Nem kerül lakkozásra";

                    var unknowBtn = new Button();
                    unknowBtn.Focusable = false;
                    unknowBtn.FontWeight = FontWeights.ExtraBold;
                    unknowBtn.Click += semiFinishedProductsResult;
                    unknowBtn.Background = Brushes.Goldenrod;
                    unknowBtn.Foreground = Brushes.White;
                    unknowBtn.Content = "Egyéb";

                    panel.Children.Add(okBtn);
                    panel.Children.Add(nokBtn);
                    panel.Children.Add(unknowBtn);

                    (sender as Expander).Content = panel;
                }
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt az adatbázisnál! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
            }
            
        }

        private void semiFinishedProductsResult(object sender, RoutedEventArgs e)
        {
            string prodDm = ((sender as Button).Parent as StackPanel).Name.ToString();
            string type = "";

            foreach (var item in ((sender as Button).Parent as StackPanel).Children)
            {   
                if (item is TextBlock)
                {
                    if ((item as TextBlock).Text.Contains("VOLVO"))
                    {
                        type = "VOLVO";
                    }
                    else if ((item as TextBlock).Text.Contains("BMW"))
                    {
                        type = "BMW";
                    }
                    else
                    {
                        type = "n/a";
                    }
                }
            } 

            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;

            try
            {
                using (var conn = new NpgsqlConnection(connstring))
                {
                    conn.Open();

                    if (Convert.ToInt32(new NpgsqlCommand("SELECT count(*) FROM semi_finished_products WHERE prod_dm = '" + prodDm + "'",conn).ExecuteScalar()) == 0)
                        new NpgsqlCommand("INSERT INTO semi_finished_products(prod_dm,product_type,result)Values('"+prodDm+"','"+type+"','"+(sender as Button).Content+"')", conn).ExecuteNonQuery();

                    IsExpandOpen = 0;
                    RefreshSemiFinishedProducts();
                }
            }
            catch(Exception msg)
            {
                MsgBoxShow("Hiba történt az adatbázisnál! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
            }
        }

        private void semiFinishedProductResetBtn_Click(object sender, RoutedEventArgs e)
        {
            
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;

            try
            {
                using (var conn = new NpgsqlConnection(connstring))
                {
                    conn.Open();

                    string machineName = Environment.MachineName.ToString();
                    if (machineName == "DESKTOP-7L1HPPN")
                        machineName = "old_lakk_pc";

                    string selectQuery = @"(SELECT bmw.prod_dm,'BMW','RESET' FROM
                                (
	                                SELECT COUNT(*) AS db,prod_dm FROM bmw GROUP BY prod_dm
                                ) AS subq
                                JOIN bmw ON bmw.prod_dm = subq.prod_dm
                                WHERE db = 1 AND workstation = '" + machineName + @"' AND bmw.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))

                                UNION

                                (SELECT volvo.prod_dm,'VOLVO','RESET' FROM
                                (
	                                SELECT COUNT(*) AS db,prod_dm FROM volvo GROUP BY prod_dm
                                ) AS subq
                                JOIN volvo ON volvo.prod_dm = subq.prod_dm
                                WHERE db = 1 AND workstation = '" + machineName + @"' AND volvo.prod_dm NOT IN (SELECT prod_dm FROM semi_finished_products))
                                ";

                    new NpgsqlCommand("INSERT INTO semi_finished_products(prod_dm,product_type,result) "+selectQuery+";", conn).ExecuteNonQuery();
                    semiFinishedProducts.Children.Clear();
                    semiFinishedCount.Text = semiFinishedProducts.Children.Count.ToString();
                    IsExpandOpen = 0;
                }
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt az adatbázisnál! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
            }
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

            //Continous Error timer
            ContinuousErrorSound.Tick += new EventHandler(Timer_Error);
            ContinuousErrorSound.Interval = new TimeSpan(0, 0, 0, 0, 550);

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
            {
                DailyProduction();
                UpdateLastShiftStatistic();
                if (IsExpandOpen == 0)
                {
                    RefreshSemiFinishedProducts();
                }
            }

            if ( (d.Hour == 6 || d.Hour == 14 || d.Hour == 22 ) && d.Minute == 0 && d.Second == 0)
            {
                //shift reset
                try
                {
                    string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;
                    var conn = new NpgsqlConnection(connstring);
                    conn.Open();

                    string machineName = Environment.MachineName.ToString();
                    if (machineName == "DESKTOP-7L1HPPN")
                        machineName = "old_lakk_pc";

                    var cmd = new NpgsqlCommand("UPDATE shift_dates SET date = to_char(current_timestamp,'YYYY-MM-DD HH24:MI:SS') where workstation = '" + machineName + "'", conn);
                    cmd.ExecuteNonQuery();

                    new NpgsqlCommand("UPDATE shift_dates SET shift_stat_bmw = "+int.Parse(BMWtodayShift.Content.ToString()) + " WHERE workstation = '" + machineName + "'", conn).ExecuteNonQuery();
                    new NpgsqlCommand("UPDATE shift_dates SET shift_stat_volvo = " + int.Parse(VOLVOtodayShift.Content.ToString()) + " WHERE workstation = '" + machineName + "'", conn).ExecuteNonQuery();

                    conn.Close();

                    DailyProduction();
                    UpdateLastShiftStatistic();

                }
                catch (Exception msg)
                {
                    MsgBoxShow("Hiba történt az adatbázisnál! Részletek elmentve az Errors mappába!", false);
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                }
            }
        }

        

        //Error timer
        private void Timer_Error(object sender, EventArgs e)
        {
            if (IsLeaderApprovalNeeded)
            {
                ErrorSound(1);
                ChangeBackground();
            }
            else
            {
                ContinuousErrorSound.Stop();
                mainStackPanel.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFDFDFD"));
            }
        }

        private void ChangeBackground()
        {

            if (mainStackPanel.Background.ToString() == "#FFFDFDFD")
            {
                productLbl.Foreground = Brushes.White;
                dbresultLbl.Foreground = Brushes.White;
                mainStackPanel.Background = Brushes.Red;
            }
            else
            {
                productLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                dbresultLbl.Foreground = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFF0505"));
                mainStackPanel.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFFDFDFD"));
            }
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
                if (Interlock != "ON")
                    return true;

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
                    cnn.Close();

                    if (dtm.Rows.Count.ToString() == "1")
                    {
                        string mbid = dtm.Rows[0]["MainBoard_ID"].ToString();
                        heatsinkID = productTxbx.Text;
                        mainboardID = mbid;
                        productTxbx.Text = mainboardID;
                        ProductValidator();
                        return true;
                    }
                    else throw new KeyNotFoundException("The HeatsinkID was not found in deltaTecServer's database!");
                }
                catch (KeyNotFoundException msg)
                {
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                    dbresultLbl.Text = "A TERMÉK NINCS TESZTELVE! NE LAKKOZD, SZÓLJ A MŰSZAKVEZETŐNEK!";
                    FormErrorDisplay();
                    return false;
                }
                catch (Exception msg)
                {
                    MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                    return false;
                }
            }
            else if (productTxbx.Text.Length == 24 && !IsLeaderApprovalNeeded) //if length is 24 and its a mainboardID then get the heatsingID from deltaTecServer
            {
                if (Interlock != "ON")
                    return true;

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
                    cnn.Close();

                    if (dtm.Rows.Count.ToString() == "1")
                    {
                        string mbid = dtm.Rows[0]["HeatSink_ID"].ToString();
                        heatsinkID = mbid;
                        mainboardID = productTxbx.Text;
                        return true;
                    }
                    else throw new KeyNotFoundException("The MainboardID was not found in deltaTecServer's database!");
                }
                catch (KeyNotFoundException msg)
                {
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                    dbresultLbl.Text = "A TERMÉK NINCS TESZTELVE! NE LAKKOZD, SZÓLJ A MŰSZAKVEZETŐNEK!";
                    FormErrorDisplay();
                    return false;
                }
                catch (Exception msg)
                {
                    MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                    ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
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
                    InvalidInput("NEM MEGFELELŐ VONALKÓDOT OLVASTÁL BE!");
                    ErrorSound(3);
                }

                return false;
            }
        }

        private void InvalidInput(string msg)
        {
            if (productTxbx.Text.Length != 0)
            {
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
            if (Interlock != "ON")
                return true;

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
                cnn.Close();
                if (dtm.Rows[0]["Result"].ToString() == "OK")
                    return true;
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
                dbresultLbl.Text = "A TERMÉK MINDKÉT OLDALA LAKKOZVA LETT";
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
                RefreshSemiFinishedProducts();
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
                FormCleanerOnUploadFinished();
                ErrorSound(3);
            }
        }
        private void UpdateLastShiftStatistic()
        {
            string connstring = ConfigurationManager.ConnectionStrings["CCTrace.CCDBConnectionString"].ConnectionString;

            string machineName = Environment.MachineName.ToString();
            if (machineName == "DESKTOP-7L1HPPN")
                machineName = "old_lakk_pc";
            try
            {
                using (var con = new NpgsqlConnection(connstring))
                {
                    con.Open();

                    string lastShiftCountHereBmw = new NpgsqlCommand("SELECT shift_stat_bmw FROM shift_dates WHERE workstation = '" + machineName + "'", con).ExecuteScalar().ToString();
                    string lastShiftCountHereVolvo = new NpgsqlCommand("SELECT shift_stat_volvo FROM shift_dates WHERE workstation = '" + machineName + "'", con).ExecuteScalar().ToString();

                    string lastShiftCountSumBmw = new NpgsqlCommand("SELECT SUM(shift_stat_bmw) FROM shift_dates", con).ExecuteScalar().ToString();
                    string lastShiftCountSumVolvo = new NpgsqlCommand("SELECT SUM(shift_stat_volvo) FROM shift_dates", con).ExecuteScalar().ToString();

                    lastShiftBmwHere.Content = lastShiftCountHereBmw;
                    lastShiftVolvoHere.Content = lastShiftCountHereVolvo;

                    lastShiftBmwSum.Content = lastShiftCountSumBmw;
                    lastShiftVolvoSum.Content = lastShiftCountSumVolvo;
                }
            }
            catch (Exception msg)
            {
                MsgBoxShow("Hiba történt! Ezt a terméket NE LAKKOZD! Részletek elmentve az Errors mappába!", false);
                ErrorLog.Create(MethodBase.GetCurrentMethod().Name.ToString(), msg.ToString(), productTxbx.Text, carrierTxbx.Text, mainboardID, heatsinkID);
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

                string machineName = Environment.MachineName.ToString();
                if (machineName == "DESKTOP-7L1HPPN")
                    machineName = "old_lakk_pc";

                //machineName = "DESKTOP-BVFFOIU";

                var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE workstation = '" + machineName + "'", conn);
                lacquerLoadCounter = Convert.ToInt32(cmd.ExecuteScalar());
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE workstation = '" + machineName + "'", conn);
                lacquerLoadCounter += Convert.ToInt32(cmd.ExecuteScalar());

                //Shift statistic

                //get the number of workstations
                int numberOfWorkstations = int.Parse(new NpgsqlCommand("SELECT COUNT(*) FROM shift_dates", conn).ExecuteScalar().ToString());

                //get the names of the workstations
                List<string> workstations = new List<string>();
                for (int i = 0; i < numberOfWorkstations; i++)
                {
                    workstations.Add(new NpgsqlCommand("SELECT workstation FROM shift_dates where id = " + (i + 1), conn).ExecuteScalar().ToString());
                }
                //sum the shift product count
                int numberOfBmwPerShift = 0;
                int numberOfVolvoPerShift = 0;

                string thisWorkstationResetDate = "";

                foreach (var mchname in workstations)
                {

                    string resetDatePerWorkstation = new NpgsqlCommand("SELECT date FROM shift_dates WHERE workstation = '" + mchname + "'", conn).ExecuteScalar().ToString();

                    if (mchname == machineName)
                    {
                        thisWorkstationResetDate = resetDatePerWorkstation;
                    }

                    numberOfBmwPerShift += int.Parse(new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE timestamp > '" + resetDatePerWorkstation + "' AND workstation = '"+ mchname + "'", conn).ExecuteScalar().ToString());
                    numberOfVolvoPerShift += int.Parse(new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE timestamp > '" + resetDatePerWorkstation + "' AND workstation = '"+ mchname + "'", conn).ExecuteScalar().ToString());
                }

                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM bmw WHERE timestamp > '"+ thisWorkstationResetDate + "' AND workstation = '" + machineName + "'", conn);
                Int32 bmwHereCountShift = Convert.ToInt32(cmd.ExecuteScalar());
                cmd = new NpgsqlCommand("SELECT COUNT(*) FROM volvo WHERE timestamp > '"+ thisWorkstationResetDate + "' AND workstation = '" + machineName + "'", conn);
                Int32 volvoHereCountShift = Convert.ToInt32(cmd.ExecuteScalar());


                conn.Close();

                //shift stat
                BMWsumShift.Content = numberOfBmwPerShift.ToString();
                VOLVOsumShift.Content = numberOfVolvoPerShift.ToString();
                VOLVOtodayShift.Content = volvoHereCountShift.ToString();
                BMWtodayShift.Content = bmwHereCountShift.ToString();
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

            if (needapproval)
                ContinuousErrorSound.Start();
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
