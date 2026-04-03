<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ViewRequests.aspx.cs" Inherits="VCBooking.ViewRequests"
    Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>My VC Requests</title>

    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />

    <style>
        .table thead th {
            background-color: dodgerblue !important;
            color: white !important;
        }
    </style>
</head>

<body>
    <form id="form1" runat="server">

        <div class="container mt-4">

            <div class="d-flex justify-content-between align-items-center mb-3">
                <h4 class="fw-bold text-primary mb-0">My VC Requests</h4>

                <a href="EmployeeDashboard.aspx" class="btn btn-outline-secondary btn-sm">&#8592; Back</a>
            </div>

            <asp:GridView ID="gvRequests" runat="server"
                CssClass="table table-bordered"
                AutoGenerateColumns="false"
                OnRowCommand="gvRequests_RowCommand">

                <Columns>
                    <asp:BoundField DataField="VCId" HeaderText="VC ID" />
                    <asp:BoundField DataField="CompanyName" HeaderText="Company" />
                    <asp:BoundField DataField="VCTypeName" HeaderText="VC Type" />
                    <asp:BoundField DataField="VCAccountName" HeaderText="VC Account" />
                    <asp:BoundField DataField="Topic" HeaderText="Topic" />
                    <asp:BoundField DataField="VCDate" HeaderText="Date" DataFormatString="{0:dd-MMM-yyyy}" />
                    <asp:BoundField DataField="FromTime" HeaderText="From" DataFormatString="{0:hh:mm tt}" />
                    <asp:BoundField DataField="ToTime" HeaderText="To" DataFormatString="{0:hh:mm tt}" />
                    <asp:BoundField DataField="LocationName" HeaderText="Location" />
                    <asp:BoundField DataField="VCStatus" HeaderText="Status" />

                    <asp:TemplateField HeaderText="Action">
                        <ItemTemplate>
                            <asp:Button ID="btnCancel" runat="server"
                                Text="Cancel"
                                CssClass="btn btn-danger btn-sm"
                                CommandName="CancelMeeting"
                                CommandArgument='<%# Eval("VCId") %>'
                                Visible='<%# Eval("VCStatus").ToString() != "Cancelled" && Eval("VCStatus").ToString() != "Deleted" %>' />
                        </ItemTemplate>
                    </asp:TemplateField>

                </Columns>
            </asp:GridView>

        </div>

        <!-- 🔥 CANCEL MODAL -->
        <div class="modal fade" id="cancelModal" tabindex="-1">
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content p-4">

                    <h5 class="text-danger">Cancel Meeting</h5>

                    <asp:TextBox ID="txtCancelReason" runat="server"
                        CssClass="form-control mt-2"
                        TextMode="MultiLine"
                        Rows="3"
                        placeholder="Enter reason (optional)" />

                    <div class="mt-3 text-end">
                        <asp:Button ID="btnConfirmCancel" runat="server"
                            Text="Confirm"
                            CssClass="btn btn-danger"
                            OnClick="btnConfirmCancel_Click" />
                    </div>

                </div>
            </div>
        </div>

        <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>

        <script>
            function showCancelModal() {
                var modal = new bootstrap.Modal(document.getElementById('cancelModal'));
                modal.show();
            }
        </script>

    </form>
</body>
</html>
