using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using InTheHand.Net; // e.g. BluetoothAddress, BluetoothEndPoint etc
using InTheHand.Net.Sockets; // e.g. BluetoothDeviceInfo, BluetoothClient, BluetoothListener
using InTheHand.Net.Bluetooth; // e.g. BluetoothService, BluetoothRadio
using System.Net;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.Linq;

namespace GUIBluetoothFinder {

    public partial class BluetoothFinderForm : Form {
        private DataSet dataSet;
        private DataTable dataTable;

        public BluetoothFinderForm() {
            InitializeComponent();
        }

        private void bluetoothFinderForm_Load(object sender, EventArgs e) {
            dataSet = new DataSet();
            dataTable = new DataTable("Device");

            // Create a new DataTable and set two DataColumn objects as primary keys.
            DataColumn[] keys = new DataColumn[1];
            DataColumn column;

            // Create column 1.
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "Device Name";

            // Add the column to the DataTable.Columns collection.
            dataTable.Columns.Add(column);

            // Create column 3.
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "Phone Number";

            // Add the column to the DataTable.Columns collection.
            dataTable.Columns.Add(column);

            // Create column 2 and add it to the array.
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "MAC Address";
            column.ReadOnly = true;

            dataTable.Columns.Add(column);

            // Add the column to the array.
            keys[0] = column;

            // Create column 2 and add it to the array.
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Boolean");
            column.ColumnName = "Synced?";

            dataTable.Columns.Add(column);

            // Set the PrimaryKeys property to the array.
            dataTable.PrimaryKey = keys;

            // add a ColumnChanged event handler for the table.
            dataTable.ColumnChanged += new DataColumnChangeEventHandler(column_Changed);

            dataSet.Tables.Add(dataTable);
            dataGridView1.DataSource = dataSet.Tables[0];

            // Create a new XmlDocument  
            XmlDocument doc = new XmlDocument();

            // Load data  
            doc.Load("http://teethtracker.heroku.com/devices.xml");

            // Get nodes
            XmlNodeList nodes = doc.SelectNodes("/devices/device");

            foreach (XmlNode node in nodes) {
                dataTable.Rows.Add(node.SelectSingleNode("device-name").InnerText, 
                            node.SelectSingleNode("local-device-number").InnerText, 
                            node.SelectSingleNode("bluetooth-id").InnerText,
                            true);                               
            }

            buildTree();
        }

        private void btnFind_Click(object sender, EventArgs e)
        {
            DiscoDevicesAsync();
            btnFind.Enabled = false;
            btnSend.Enabled = false;
            btnClear.Enabled = false;
        }

        public void DiscoDevicesAsync() {

            BluetoothComponent btComponent = new BluetoothComponent();
            btComponent.DiscoverDevicesProgress += HandleDiscoDevicesProgress;
            btComponent.DiscoverDevicesComplete += HandleDiscoDevicesComplete;
            btComponent.DiscoverDevicesAsync(255, true, true, true, false, 99);
        }

        private void HandleDiscoDevicesProgress(object sender, DiscoverDevicesEventArgs e) {

            foreach (BluetoothDeviceInfo device in e.Devices) {

                object[] findTheseVals = new object[1];
                findTheseVals[0] = device.DeviceAddress.ToString();
                DataRow foundRow = dataTable.Rows.Find(findTheseVals);

                if (foundRow == null) {
                    dataTable.Rows.Add(device.DeviceName, "", device.DeviceAddress.ToString());
                }
            }
        }

        private void buildTree()
        {
            try
            {
                // Initialize the TreeView control.
                nodeTree.Nodes.Clear();
                TreeNode tNode = new TreeNode();

                XmlDocument cDom = new XmlDocument();
                cDom.LoadXml("<nodes></nodes>");

                XDocument devMovements = XDocument.Load("http://teethtracker.heroku.com/device_movements.xml");
                //XDocument devMovements = XDocument.Load("test.xml");

                var nodeList = from nodeStation in devMovements.Descendants("device-movements").Descendants("device-movement").Descendants("node") select nodeStation.Value;
                foreach (var nodeName in nodeList.Distinct())
                {
                    XmlNode nodeElement = cDom.CreateNode(XmlNodeType.Element, nodeName.ToString(), "");

                    var deviceList = from deviceStation
                                  in devMovements.Descendants("device-movements").Descendants("device-movement").Elements("device-bluetooth-id")
                                     select deviceStation.Value;

                    foreach (var deviceName in deviceList.Distinct())
                    {
                        var locationChanges = (from c in devMovements.Descendants("device-movements").Descendants("device-movement")
                                    where String.Compare((string)c.Element("device-bluetooth-id"), deviceName.ToString()) == 0  && (string)c.Element("movement-type") == "arrival"
                                    orderby (string)c.Element("updated-at")
                                    select (string)c.Element("node").Value);

                        string currentLocation = locationChanges.Last();

                        if (String.Compare(currentLocation, nodeName.ToString()) == 0)
                        {
                            XmlNode deviceElement = cDom.CreateTextNode(deviceName.ToString());
                            nodeElement.AppendChild(deviceElement);
                        }
                    }

                    cDom.DocumentElement.AppendChild(nodeElement);
                }

                nodeTree.Nodes.Add(new TreeNode(cDom.DocumentElement.Name));
                tNode = nodeTree.Nodes[0];
                AddNode(cDom.DocumentElement, tNode);

                // Create a new XmlDocument  
                XmlDocument deviceDoc = new XmlDocument();
                XmlDocument deviceTreeDoc = new XmlDocument();

                deviceTreeDoc.LoadXml("<devices></devices>");

                // Load data  
                deviceDoc.Load("http://teethtracker.heroku.com/devices.xml");

                // Get nodes
                XmlNodeList deviceNames = deviceDoc.SelectNodes("/devices/device/device-name");
                foreach (XmlNode name in deviceNames)
                {
                    XmlNode newElem = deviceTreeDoc.CreateTextNode(name.InnerText);
                    newElem.InnerText = name.InnerText;
                    deviceTreeDoc.DocumentElement.AppendChild(newElem);
                }
                
                nodeTree.Nodes.Add(new TreeNode(deviceTreeDoc.DocumentElement.Name));
                tNode = nodeTree.Nodes[1];
                AddNode(deviceTreeDoc.DocumentElement, tNode);

                //nodeTree.ExpandAll();
            }
            catch (XmlException xmlEx)
            {
                MessageBox.Show(xmlEx.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void AddNode(XmlNode inXmlNode, TreeNode inTreeNode)
        {
            XmlNode xNode;
            TreeNode tNode;
            XmlNodeList nodeList;
            int i;

            // Loop through the XML nodes until the leaf is reached.
            // Add the nodes to the TreeView during the looping process.
            if (inXmlNode.HasChildNodes)
            {
                nodeList = inXmlNode.ChildNodes;
                for (i = 0; i <= nodeList.Count - 1; i++)
                {
                    xNode = inXmlNode.ChildNodes[i];
                    inTreeNode.Nodes.Add(new TreeNode(xNode.Name));
                    tNode = inTreeNode.Nodes[i];
                    AddNode(xNode, tNode);
                }
            }
            else
            {
                // Here you need to pull the data from the XmlNode based on the
                // type of node, whether attribute values are required, and so forth.
                inTreeNode.Text = (inXmlNode.OuterXml).Trim();
            }
        }

        private void FinishAddRequest(IAsyncResult result) {

            object[] returnedParams = (object[])result.AsyncState;
            HttpWebRequest request = (HttpWebRequest)returnedParams[0];
            string id = (string)returnedParams[1];

            object[] findTheseVals = new object[1];
            findTheseVals[0] = id;
            DataRow foundRow = dataTable.Rows.Find(findTheseVals);

            if (foundRow != null) {
                foundRow.SetField(3, true);
            }
        }

        private void FinishWebRequest(IAsyncResult result)
        {
            HttpWebResponse response = (result.AsyncState as HttpWebRequest).EndGetResponse(result) as HttpWebResponse;
        }

        private void HandleDiscoDevicesComplete(object sender, DiscoverDevicesEventArgs e) {
            if (e.Cancelled) {
                Console.WriteLine("DiscoDevicesAsync cancelled.");
            } else if (e.Error != null) {
                Console.WriteLine("DiscoDevicesAsync error: {0}.", e.Error.Message);
            } else {
                Console.WriteLine("DiscoDevicesAsync complete found {0} devices.", e.Devices.Length);
            }
            btnFind.Enabled = true;
            btnSend.Enabled = true;
            btnClear.Enabled = true;
        }

        private void btnRefresh_Click(object sender, EventArgs e) {
            buildTree();
        }

        private void btnSend_Click(object sender, EventArgs e) {
            foreach (DataRow row in dataTable.Rows) {

                WebRequest request = WebRequest.Create("http://teethtracker.heroku.com/devices/new?"
                                                        + "bluetooth_id=" + row[2]
                                                        + "&name=" + row[0]
                                                        + "&number=" + row[1]
                                                       );

                request.BeginGetResponse(new AsyncCallback(FinishAddRequest), new object[] { request, row[2].ToString() });
            }
        }

        private void btnClear_Click(object sender, EventArgs e) {
            dataTable.Clear();
            WebRequest request = WebRequest.Create("http://teethtracker.heroku.com/clear-database");

            request.BeginGetResponse(new AsyncCallback(FinishWebRequest), request);
        }

        private static void column_Changed(object sender, DataColumnChangeEventArgs e) {
            if (e.Column.ColumnName == "Phone Number") {
                e.Row[3] = false;
            }
        }
       
    }
}
