<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ViewRequests.aspx.cs" Inherits="VCBooking.ViewRequests"
    Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
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

                <a href="EmployeeDashboard.aspx" class="btn btn-outline-secondary btn-sm">&lt; Back</a>
            </div>

            <asp:GridView ID="gvRequests" runat="server"
                CssClass="table table-bordered table-hover text-center align-middle mb-0"
                AutoGenerateColumns="false"
                OnRowCommand="gvRequests_RowCommand"
                OnRowDataBound="gvRequests_RowDataBound">

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
                                UseSubmitBehavior="false"
                                CausesValidation="false"
                                Visible='<%# Eval("VCStatus").ToString() != "Cancelled" && Eval("VCStatus").ToString() != "Deleted" %>' />
                        </ItemTemplate>
                    </asp:TemplateField>

                </Columns>
            </asp:GridView>

            <!-- Empty State Message -->
            <div id="divEmptyState" runat="server" visible="false" class="text-center py-5 bg-light border rounded">
                <div class="mb-3">
                    <span style="font-size: 3rem; color: #6c757d;">&#128197;</span>
                </div>
                <h5 class="text-secondary fw-bold">No Meeting Requests Found</h5>
                <p class="text-muted">You haven't made any video conference requests yet.</p>
                <a href="CreateVCRequest.aspx" class="btn btn-primary mt-2">Create New Request</a>
            </div>

        </div>

        <!-- Cancel Modal -->
        <div class="modal fade" id="cancelModal" tabindex="-1">
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content shadow">
                    <div class="modal-header bg-danger text-white">
                        <h5 class="modal-title">Cancel Meeting</h5>
                        <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body text-start">
                        <p>Are you sure you want to cancel this meeting?</p>
                        <div class="mb-3">
                            <label class="form-label">Reason for Cancellation:</label>
                            <asp:TextBox ID="txtCancelReason" runat="server"
                                CssClass="form-control"
                                TextMode="MultiLine"
                                Rows="3"
                                placeholder="Enter reason (optional)"></asp:TextBox>
                        </div>
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                        <asp:Button ID="btnConfirmCancel" runat="server"
                            Text="Confirm Cancellation"
                            CssClass="btn btn-danger"
                            OnClick="btnConfirmCancel_Click"
                            UseSubmitBehavior="false"
                            OnClientClick="var btn=this; setTimeout(function(){ btn.disabled=true; btn.value='Cancelling...'; },10);" />
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
