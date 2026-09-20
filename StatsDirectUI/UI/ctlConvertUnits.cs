using System;
using System.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Expressions;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.UI
{
    public partial class ctlConvertUnits : IFillParameterBag
    {
        private string lastCalculationAsString;

        public ctlConvertUnits()
        {
            InitializeComponent();
            SetupConversions();
        }

        private void SetupConversions()
        {
            object[] conversions = {
                new Conversion("X*76", "CM of Mercury (0&#176;C)", "Atmospheres to CM of Mercury (0&#176;C)"),
                new Conversion("X*29.921", "Inches of Mercury (32&#176;F)", "Atmospheres to Inches of Mercury (32&#176;F)"),
                new Conversion("X*2116.32", "Pounds/sq ft", "Atmospheres to Pounds/sq ft"),
                new Conversion("X*14.696", "Pounds/sq ft", "Atmospheres to Pounds/sq ft"),
                new Conversion("X*42", "Gallon (US)", "Barrels, oil to Gallon (US)"),
                new Conversion("X*0.159", "Meter&#179;", "Barrels (API) to Meter&#179;"),
                new Conversion("X*1055", "Joule", "BTU (15.56&#176;C) to Joule"),
                new Conversion("X*0.0236", "Horsepower", "BTU/Minute to Horsepower"),
                new Conversion("X*4.19", "Joule", "Calories (mean) to Joule"),
                new Conversion("X*0.3937", "Inches", "Centimetres to Inches"),
                new Conversion("X*0.061", "Cubic inches", "Cubic centimetres to Cubic inches"),
                new Conversion("X*0.00022", "Gallons (British)", "Cubic Centimetres to Gallons (British)"),
                new Conversion("X*0.00026", "Gallons (US)", "Cubic Centimetres to Gallons (US)"),
                new Conversion("X*0.037", "Cubic Yards (US)", "Cubic Feet to Cubic Yards (US)"),
                new Conversion("X*6.2288", "Gallons (British)", "Cubic Feet to Gallons (British)"),
                new Conversion("X*7.4805", "Gallons (US)", "Cubic Feet to Gallons (US)"),
                new Conversion("X*28.3162", "Litres", "Cubic Feet to Litres"),
                new Conversion("X*16.3872", "Cubic Cm", "Cubic Inches to Cubic Cm"),
                new Conversion("X*35.314", "Cubic Feet", "Cubic Meters to Cubic Feet"),
                new Conversion("X*219.969", "Gallons (British)", "Cubic Meters to Gallons (British)"),
                new Conversion("X*264.173", "Gallons (US)", "Cubic Meters to Gallons (US)"),
                new Conversion("(X-32)/1.8", "Degrees (C)", "Degrees (F) to Degrees (C)"),
                new Conversion("(X*1.8)+32", "Degrees (F)", "Degrees (C) to Degrees (F)"),
                new Conversion("X*0.3048", "Meters", "Feet to Meters"),
                new Conversion("X*0.8826", "Inches of Mercury (32&#176;F)", "Feet of Water (39.2&#176;F) to Inches of Mercury (32&#176;F)"),
                new Conversion("X*62.427", "Pounds/sq ft", "Feet of Water (39.2&#176;F) to Pounds/sq ft"),
                new Conversion("X*0.0183", "Kilometres/hour", "Feet/minute to Kilometres/hour"),
                new Conversion("X*0.005", "Meters/second", "Feet/minute to Meters/second"),
                new Conversion("X*0.1605", "Cubic Ft", "Gallons (British) to Cubic Ft"),
                new Conversion("X*1.2009", "Gallons(US)", "Gallons (British) to Gallons(US)"),
                new Conversion("X*4.5459", "Litres", "Gallons (British) to Litres"),
                new Conversion("X*0.1337", "Cubic Ft", "Gallons (US) to Cubic Ft"),
                new Conversion("X*0.8327", "Gallons(British)", "Gallons (US) to Gallons(British)"),
                new Conversion("X*3.7853", "Litres", "Gallons (US) to Litres"),
                new Conversion("X*0.03527", "Ounces", "Grams to Ounces"),
                new Conversion("X*1.014", "Cheval-Vapeur", "Horsepower to Cheval-Vapeur"),
                new Conversion("X*745.7", "Watts", "Horsepower to Watts"),
                new Conversion("X*2.54", "Centimetres", "Inches to Centimetres"),
                new Conversion("X*0.0334", "Atmospheres", "Inches of Mercury (32&#176;F) to Atmospheres"),
                new Conversion("X*2.2046", "Pounds", "Kilograms to Pounds"),
                new Conversion("X*3280", "Feet", "Kilometres to Feet"),
                new Conversion("X*0.6213", "Miles", "Kilometres to Miles"),
                new Conversion("X*56.884", "BTU/Minute", "Kilowatts to BTU/Minute"),
                new Conversion("X*0.0353", "Cubic Feet", "Litres to Cubic Feet"),
                new Conversion("X*0.2199", "Gallons (British)", "Litres to Gallons (British)"),
                new Conversion("X*0.2641", "Gallons (US)", "Litres to Gallons (US)"),
                new Conversion("X*3.2808", "Feet", "Meters to Feet"),
                new Conversion("X*5280", "Feet", "Miles to Feet"),
                new Conversion("X*1.6093", "Kilometres", "Miles to Kilometres"),
                new Conversion("X*320", "Rods", "Miles to Rods"),
                new Conversion("X*6080", "Feet", "Miles (nautical) to Feet"),
                new Conversion("X*0.035", "Ounces (Fluid-British)", "Millilitres to Ounces (Fluid-British)"),
                new Conversion("X*0.0338", "Ounces (Fluid-US)", "Millilitres to Ounces (Fluid-US)"),
                new Conversion("X*0.039", "Inches", "Millimetres to Inches"),
                new Conversion("X*28.3495", "Grams", "Ounces to Grams"),
                new Conversion("X*28.413", "Cubic centimetres", "Ounces (British) to Cubic centimetres"),
                new Conversion("X*29.5737", "Cubic centimetres", "Ounces (US) to Cubic centimetres"),
                new Conversion("X*453.5924", "Grams", "Pounds to Grams"),
                new Conversion("X*0.000472", "Atmospheres", "Pounds/sq ft to Atmospheres"),
                new Conversion("X*0.068", "Atmospheres", "Pounds/sq in to Atmospheres"),
                new Conversion("X*2.036", "Inches of Mercury (32&#176;F)", "Pounds/sq in to Inches of Mercury (32&#176;F)"),
                new Conversion("X*1136.521", "Cubic centimetres", "Quarts (British) to Cubic centimetres"),
                new Conversion("X*946.3586", "Cubic centimetres", "Quarts (US) to Cubic centimetres"),
                new Conversion("X*1016.047", "Kilograms", "Tons (long) to Kilograms"),
                new Conversion("X*2240", "Pounds", "Tons (long) to Pounds"),
                new Conversion("X*1.016", "Tons (metric)", "Tons (long) to Tons (metric)"),
                new Conversion("X*1.12", "Tons (short)", "Tons (long) to Tons (short)"),
                new Conversion("X*91.44", "Centimetres", "Yards to Centimetres")
            };
            cboConversion.Items.AddRange(conversions);
        }

        private void Calc_Click(object sender, EventArgs e)
        {
            Calculate();
        }

        private void DoubleClickTextbox(object sender, EventArgs e)
        {
            Calculate();
        }

        private void edpdf_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                Calculate();
            }
        }

        private void Calculate()
        {
            double from = Parsing.Cdbl_Txt(txtFrom.Text);
            if (Constant.MISSING == from)
                txtTo.Text = Formatting.ASTERISK;
            else
            {
                if (cboConversion.SelectedItem is not Conversion conversion)
                    return; // no conversion chosen yet
                try
                {
                    // the built-in conversions are written with a decimal point: read them so whatever the regional settings
                    Calcit c = new(conversion.Expression, new[] { DataType.Double }, false, true);
                    double[] x = new double[1];
                    x[0] = from;
                    txtTo.Text = c.Evaluate<object>(x).ToString();
                    lastCalculationAsString = txtFrom.Text + " as " + conversion.ResultUnit + " = " + txtTo.Text;
                }
                catch (Exception)
                {
                    // an error here used to reach the application's handler for unhandled errors, which closes the program
                    txtTo.Text = Formatting.ASTERISK;
                }
            }
        }

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            outputParameters.AddOutput("gr", lastCalculationAsString);
            return null;
        }
    }
}