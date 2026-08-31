using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Hase.Client.Wpf.RfLab
{
    /// <summary>
    /// Interaction logic for RFLab_UC_Dial.xaml
    /// </summary>
    public partial class RFLab_UC_Dial : UserControl, INotifyPropertyChanged
    {
        private PropertyChangedEventHandler _PropertyChanged;
        public virtual event PropertyChangedEventHandler PropertyChanged
        {
            add { _PropertyChanged += value; }
            remove { _PropertyChanged -= value; }
        }
        public void Changed(string propertyName)
        {
            if (_PropertyChanged != null)
            {
                _PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }


        Grid DialGrid;

        int NoOfDigits = 1;
        RFLabDigit[] Digits;
        RFLabDigit[] AmplitudeDigits;

        private RFLabDigit _selectedDigit = null;
        public RFLabDigit SelectedDigit
        {
            get { return _selectedDigit; }
            set 
            {
                if (_selectedDigit != null) _selectedDigit.IsSelected = false;
                _selectedDigit = value;
                if (_selectedDigit != null) _selectedDigit.IsSelected = true;
                Changed(nameof(SelectedDigit)); 
            }
        }


        public RFLab_UC_Dial()
        {
            DataContext = this;
            InitializeComponent();
            createDial(9);
        }

        public void createDial(int noOfDigits)
        {
            NoOfDigits = noOfDigits;
            Digits = new RFLabDigit[NoOfDigits];
            AmplitudeDigits = new RFLabDigit[2];

            DialGrid = new Grid();
            DialGrid.Width = 390;
            DialGrid.Height = 75;
            RowDefinition gridRow0 = new RowDefinition();
            gridRow0.Height = new GridLength(50);
            DialGrid.RowDefinitions.Add(gridRow0);
            RowDefinition gridRow1 = new RowDefinition();
            gridRow1.Height = new GridLength(3);
            DialGrid.RowDefinitions.Add(gridRow1);
            RowDefinition gridRow2 = new RowDefinition();
            gridRow2.Height = new GridLength(20);
            DialGrid.RowDefinitions.Add(gridRow2);

            int columnCount = 0;
            for (int i = (NoOfDigits-1); i >= 0; i--)
            {
                ColumnDefinition gridCol = new ColumnDefinition();
                gridCol.Width = new GridLength(34);
                DialGrid.ColumnDefinitions.Add(gridCol);
                Label digitLabel = new Label();
                digitLabel.FontSize = 32;
                digitLabel.FontWeight = FontWeights.Bold;
                digitLabel.Foreground = RFLabDigit.DigitForeground;
                digitLabel.Background = RFLabDigit.DigitBackground;
                digitLabel.VerticalAlignment = VerticalAlignment.Stretch;
                digitLabel.VerticalContentAlignment = VerticalAlignment.Center;
                digitLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
                digitLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
                Grid.SetColumn(digitLabel, columnCount++);
                Grid.SetRow(digitLabel, 0);
                RFLabDigit rflabDigit = new RFLabDigit(i, 0, (int)Math.Pow(10, i));
                Digits[i] = rflabDigit;
                digitLabel.Tag = rflabDigit;
                digitLabel.Content = rflabDigit.Value.ToString();
                digitLabel.MouseDown += new MouseButtonEventHandler(DigitSelectionHandler);
                digitLabel.MouseWheel += new MouseWheelEventHandler(DigitChangeHandler);
                DialGrid.Children.Add(digitLabel);

                if (i % 3 == 0)
                {
                    ColumnDefinition gridColSeparator = new ColumnDefinition();
                    gridColSeparator.Width = (i == 0) ? new GridLength(10) : new GridLength(2);
                    DialGrid.ColumnDefinitions.Add(gridColSeparator);
                    Label separatorLabel = new Label();
                    separatorLabel.FontSize = 32;
                    separatorLabel.FontWeight = FontWeights.Bold;
                    separatorLabel.Foreground = RFLabDigit.DigitForeground;
                    separatorLabel.Background = RFLabDigit.SeparatorBackground;
                    separatorLabel.VerticalAlignment = VerticalAlignment.Stretch;
                    separatorLabel.VerticalContentAlignment = VerticalAlignment.Center;
                    separatorLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    separatorLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
                    Grid.SetColumn(separatorLabel, columnCount++);
                    Grid.SetRow(separatorLabel, 0);
                    separatorLabel.Content = RFLabDigit.Separator;
                    DialGrid.Children.Add(separatorLabel);
                }
            }

            ColumnDefinition gridAColA = new ColumnDefinition();
            gridAColA.Width = new GridLength(34);
            DialGrid.ColumnDefinitions.Add(gridAColA);
            Label amplitudeLabel = new Label();
            amplitudeLabel.FontSize = 32;
            amplitudeLabel.FontWeight = FontWeights.Bold;
            amplitudeLabel.Foreground = RFLabDigit.ADigitForeground;
            amplitudeLabel.Background = RFLabDigit.ADigitBackground;
            amplitudeLabel.VerticalAlignment = VerticalAlignment.Stretch;
            amplitudeLabel.VerticalContentAlignment = VerticalAlignment.Center;
            amplitudeLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            amplitudeLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(amplitudeLabel, columnCount++);
            Grid.SetRow(amplitudeLabel, 0);
            RFLabDigit rflabADigit = new RFLabDigit(1, 0, (int)Math.Pow(10, 0));
            AmplitudeDigits[1] = rflabADigit;
            amplitudeLabel.Tag = rflabADigit;
            amplitudeLabel.Content = rflabADigit.Value.ToString();
            amplitudeLabel.MouseDown += new MouseButtonEventHandler(DigitSelectionHandler);
            amplitudeLabel.MouseWheel += new MouseWheelEventHandler(DigitChangeHandler);
            DialGrid.Children.Add(amplitudeLabel);

            gridAColA = new ColumnDefinition();
            gridAColA.Width = new GridLength(34);
            DialGrid.ColumnDefinitions.Add(gridAColA);
            amplitudeLabel = new Label();
            amplitudeLabel.FontSize = 32;
            amplitudeLabel.FontWeight = FontWeights.Bold;
            amplitudeLabel.Foreground = RFLabDigit.ADigitForeground;
            amplitudeLabel.Background = RFLabDigit.ADigitBackground;
            amplitudeLabel.VerticalAlignment = VerticalAlignment.Stretch;
            amplitudeLabel.VerticalContentAlignment = VerticalAlignment.Center;
            amplitudeLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            amplitudeLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(amplitudeLabel, columnCount++);
            Grid.SetRow(amplitudeLabel, 0);
            rflabADigit = new RFLabDigit(0, 0, (int)Math.Pow(10, 0));
            AmplitudeDigits[0] = rflabADigit;
            amplitudeLabel.Tag = rflabADigit;
            amplitudeLabel.Content = rflabADigit.Value.ToString();
            amplitudeLabel.MouseDown += new MouseButtonEventHandler(DigitSelectionHandler);
            amplitudeLabel.MouseWheel += new MouseWheelEventHandler(DigitChangeHandler);
            DialGrid.Children.Add(amplitudeLabel);

            Label mhzLabel = new Label();
            mhzLabel.FontSize = 10;
            mhzLabel.FontWeight = FontWeights.Normal;
            mhzLabel.Foreground = Brushes.White;
            mhzLabel.Background = Brushes.Black;
            mhzLabel.VerticalAlignment = VerticalAlignment.Stretch;
            mhzLabel.VerticalContentAlignment = VerticalAlignment.Center;
            mhzLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            mhzLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(mhzLabel, 0); Grid.SetColumnSpan(mhzLabel,3);
            Grid.SetRow(mhzLabel, 2);
            mhzLabel.Content = "|---- MHz ----|";
            DialGrid.Children.Add(mhzLabel);

            Label kHzLabel = new Label();
            kHzLabel.FontSize = 10;
            kHzLabel.FontWeight = FontWeights.Normal;
            kHzLabel.Foreground = Brushes.White;
            kHzLabel.Background = Brushes.Black;
            kHzLabel.VerticalAlignment = VerticalAlignment.Stretch;
            kHzLabel.VerticalContentAlignment = VerticalAlignment.Center;
            kHzLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            kHzLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(kHzLabel, 4); Grid.SetColumnSpan(kHzLabel, 3);
            Grid.SetRow(kHzLabel, 2);
            kHzLabel.Content = "|---- KHz ----|";
            DialGrid.Children.Add(kHzLabel);

            Label HzLabel = new Label();
            HzLabel.FontSize = 10;
            HzLabel.FontWeight = FontWeights.Normal;
            HzLabel.Foreground = Brushes.White;
            HzLabel.Background = Brushes.Black;
            HzLabel.VerticalAlignment = VerticalAlignment.Stretch;
            HzLabel.VerticalContentAlignment = VerticalAlignment.Center;
            HzLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            HzLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(HzLabel, 8); Grid.SetColumnSpan(HzLabel, 3);
            Grid.SetRow(HzLabel, 2);
            HzLabel.Content = "|---- Hz ----|";
            DialGrid.Children.Add(HzLabel);

            Label dBLabel = new Label();
            dBLabel.FontSize = 10;
            dBLabel.FontWeight = FontWeights.Normal;
            dBLabel.Foreground = Brushes.White;
            dBLabel.Background = Brushes.Black;
            dBLabel.VerticalAlignment = VerticalAlignment.Stretch;
            dBLabel.VerticalContentAlignment = VerticalAlignment.Center;
            dBLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            dBLabel.HorizontalContentAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(dBLabel, 12); Grid.SetColumnSpan(dBLabel, 2);
            Grid.SetRow(dBLabel, 2);
            dBLabel.Content = "|- -dB -|";
            DialGrid.Children.Add(dBLabel);

            // Finish:
            DialBorder.Child = DialGrid;
            SelectedDigit = null;
        }

        private Label SelectedLabel = null;
        void DigitSelectionHandler(object sender, MouseButtonEventArgs e)
        {
            if (sender is Label) 
            {
                if (SelectedLabel != null)
                {
                        SelectedDigit.IsSelected = false;
                        if (Digits.Contains(SelectedDigit))
                        {
                            SelectedLabel.Background = RFLabDigit.DigitBackground;
                        }
                        else
                        {
                            SelectedLabel.Background = RFLabDigit.ADigitBackground;
                        }
                }
                 
                SelectedLabel = (Label)sender;
                if (SelectedLabel.Tag is RFLabDigit)
                {
                    SelectedDigit = (RFLabDigit)SelectedLabel.Tag;
                    if (Digits.Contains(SelectedDigit))
                    {
                        SelectedLabel.Background = RFLabDigit.SelectedDigitBackground;
                    }
                    else
                    {
                        SelectedLabel.Background = RFLabDigit.SelectedADigitBackground;
                    }
                }             
            }
        }

        void DigitChangeHandler(object sender, MouseWheelEventArgs e)
        {
            if (sender is Label)
            {
                SelectedLabel = (Label)sender;
                if (SelectedLabel.Tag is RFLabDigit)
                {
                    SelectedDigit = (RFLabDigit)SelectedLabel.Tag;
                    if (Digits.Contains(SelectedDigit))
                    {
                        e.Handled = true;
                    }
                    else
                    {
                        e.Handled = true;
                    }
                }
            }
        }

    }

    public class RFLabDigit
    {
        public static SolidColorBrush DigitForeground = Brushes.White;
        public static SolidColorBrush ADigitForeground = Brushes.Blue;
        public static SolidColorBrush DigitBackground = Brushes.Maroon;
        public static SolidColorBrush ADigitBackground = Brushes.WhiteSmoke;
        public static SolidColorBrush SelectedDigitBackground = Brushes.DodgerBlue;
        public static SolidColorBrush SelectedADigitBackground = Brushes.Goldenrod;
        public static SolidColorBrush SeparatorBackground = Brushes.Gray;
        public static string Separator = ".";
        public int Value { get; set; } = 0;
        public int Index { get; set; } = 0;
        public int Multiplicator { get; set; } = 1;
        public bool IsSelected { get; set; } = false;

        public RFLabDigit() { }

        public RFLabDigit(int idx, int val, int multiplicator, bool isSelected = false)
        {
            Index = idx;
            Value = val;
            Multiplicator = multiplicator;
            IsSelected = isSelected;
        }
    }
}
