using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RenTradeWindowForm.Helper
{
    public static class DialogBox
    {
        public static DialogResult ShowInputDialogBox(ref string input, string prompt, string title = "Title", int width = 300, int height = 200)
        {
            //This function creates the custom input dialog box by individually creating the different window elements and adding them to the dialog box

            //Specify the size of the window using the parameters passed
            Size size = new Size(width, height);
            //Create a new form using a System.Windows Form
            Form inputBox = new()
            {
                ControlBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true,
                ClientSize = size,
                Text = title,
                ShowInTaskbar = false
            };

            //Create a new label to hold the prompt
            Label label = new()
            {
                Text = prompt,
                Location = new Point(5, 10),
                Width = size.Width - 10
            };
            inputBox.Controls.Add(label);

            //Create a textbox to accept the user's input
            TextBox textBox = new()
            {
                Size = new Size(size.Width - 10, 30),
                Location = new Point(5, label.Location.Y + 25),
                BorderStyle = BorderStyle.Fixed3D,
                Font = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Point),
                Multiline = true,
                Text = input
            };
            inputBox.Controls.Add(textBox);

            //Create an OK Button 
            Button okButton = new() 
            {

                DialogResult = DialogResult.OK,
                Name = "okButton",
                Size = new Size(75, 23),
                Text = "&OK",
                Location = new Point(size.Width - 80, size.Height - 30)
            };
            inputBox.Controls.Add(okButton);

            ////Create a Cancel Button
            //Button cancelButton = new Button();
            //cancelButton.DialogResult = DialogResult.Cancel;
            //cancelButton.Name = "cancelButton";
            //cancelButton.Size = new Size(75, 23);
            //cancelButton.Text = "&Cancel";
            //cancelButton.Location = new Point(size.Width - 80, size.Height - 30);
            //inputBox.Controls.Add(cancelButton);

            //Set the input box's buttons to the created OK and Cancel Buttons respectively so the window appropriately behaves with the button clicks
            inputBox.AcceptButton = okButton;
            //inputBox.CancelButton = cancelButton;

            //Show the window dialog box 
            DialogResult result = inputBox.ShowDialog();
            input = textBox.Text;

            //After input has been submitted, return the input value
            return result;
        }
    }
}
