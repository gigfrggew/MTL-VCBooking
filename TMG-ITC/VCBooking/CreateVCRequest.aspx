<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="CreateVCRequest.aspx.cs" Inherits="VCBooking.CreateVCRequest" Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Create VC Request - VC Booking</title>
    <link href="../Content/bootstrap.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link href="../Content/Site.css?v=20260427-clean3" rel="stylesheet" />
    <script src="../Scripts/bootstrap.bundle.min.js"></script>
    <script>
        var processingTimer;
        function showProcessingOverlay() {
            processingTimer = setTimeout(function() {
                var overlay = document.getElementById('processingOverlay');
                if (overlay) overlay.classList.add('show');
            }, 300);
        }

        function hideProcessingOverlay() {
            clearTimeout(processingTimer);
            var overlay = document.getElementById('processingOverlay');
            if (overlay) overlay.classList.remove('show');
        }

        document.addEventListener('DOMContentLoaded', function () {
            var hdn = document.getElementById('<%= hdnShowSuccess.ClientID %>');
            if (hdn && hdn.value === '1') {
                document.getElementById('processingOverlay').classList.remove('show');
                new bootstrap.Modal(document.getElementById('successModal')).show();
            }
        });
    </script>
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <asp:ScriptManager runat="server" />
        <asp:HiddenField ID="hdnShowSuccess" runat="server" Value="0" />

        <div class="rail-viewport">
            <asp:UpdatePanel runat="server" ID="UpdatePanel1">
                <ContentTemplate>
                    <div class="rail-card rail-card-wide premium-card">

                        <!-- HEADER -->
                        <div class="page-header-row">
                            <a href="EmployeeDashboard.aspx" class="nav-back-link">
                                <i class="bi bi-arrow-left"></i> Back to Menu
                            </a>
                            <h2 class="fw-bold m-0" style="color: var(--text-main);">Create VC Request</h2>
                        </div>

                        <!-- BASIC INFO: Company + Topic -->
                        <h5 class="section-title mb-4">Basic Information</h5>
                        <div class="row g-4 mb-3">
                            <div class="col-md-6">
                                <label class="text-label">Company <span class="text-danger">*</span></label>
                                <asp:DropDownList runat="server" ID="ddlCompany" CssClass="form-select form-control-premium" />
                            </div>
                            <div class="col-md-6">
                                <label class="text-label">Meeting Topic <span class="text-danger">*</span></label>
                                <asp:TextBox runat="server" ID="txtTopic" CssClass="form-control form-control-premium" placeholder="What is the meeting about?" />
                            </div>
                        </div>

                        <!-- DATE + VC TYPE + VC ACCOUNT: Horizontal row below topic -->
                        <div class="row g-4 mb-5">
                            <div class="col-md-4">
                                <label class="text-label">Booking Date <span class="text-danger">*</span></label>
                                <asp:TextBox ID="txtDate" runat="server" TextMode="Date"
                                    AutoPostBack="true" OnTextChanged="DateOrTimeChanged"
                                    CssClass="form-control form-control-premium" />
                            </div>
                            <div class="col-md-4">
                                <label class="text-label">VC Type <span class="text-danger">*</span></label>
                                <asp:DropDownList runat="server" ID="ddlVCType"
                                    AutoPostBack="true"
                                    OnSelectedIndexChanged="ddlVCType_SelectedIndexChanged"
                                    CssClass="form-select form-control-premium" />
                            </div>
                            <div class="col-md-4">
                                <label class="text-label">VC Account <span class="text-danger">*</span></label>
                                <asp:DropDownList runat="server" ID="ddlVCAccount"
                                    CssClass="form-select form-control-premium"
                                    AutoPostBack="true"
                                    OnSelectedIndexChanged="ddlVCAccount_SelectedIndexChanged" />
                            </div>
                        </div>

                        <!-- SCHEDULE: Left vertical controls | Right preview grid -->
                        <h5 class="section-title mb-4">Schedule & Availability</h5>
                        <div class="row g-4 mb-4">

                            <!-- LEFT: From Time, Duration, Location - stacked vertically -->
                            <div class="col-md-3">
                                <div class="mb-4">
                                    <label class="text-label">From Time <span class="text-danger">*</span></label>
                                    <div class="d-flex gap-2">
                                        <asp:DropDownList ID="ddlFromHour" runat="server"
                                            AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                            CssClass="form-select form-control-premium" />
                                        <asp:DropDownList ID="ddlFromMinute" runat="server"
                                            AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                            CssClass="form-select form-control-premium">
                                            <asp:ListItem Text="Min" Value="" />
                                            <asp:ListItem Text="00" Value="0" />
                                            <asp:ListItem Text="15" Value="15" />
                                            <asp:ListItem Text="30" Value="30" />
                                            <asp:ListItem Text="45" Value="45" />
                                        </asp:DropDownList>
                                    </div>
                                </div>

                                <div class="mb-4">
                                    <label class="text-label">Duration <span class="text-danger">*</span></label>
                                    <div class="d-flex gap-2">
                                        <asp:DropDownList ID="ddlHours" runat="server"
                                            AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                            CssClass="form-select form-control-premium">
                                            <asp:ListItem Text="Hr" Value="" />
                                            <asp:ListItem Text="0" Value="0" />
                                            <asp:ListItem Text="1" Value="1" />
                                            <asp:ListItem Text="2" Value="2" />
                                            <asp:ListItem Text="3" Value="3" />
                                            <asp:ListItem Text="4" Value="4" />
                                            <asp:ListItem Text="5" Value="5" />
                                            <asp:ListItem Text="6" Value="6" />
                                            <asp:ListItem Text="7" Value="7" />
                                            <asp:ListItem Text="8" Value="8" />
                                        </asp:DropDownList>
                                        <asp:DropDownList ID="ddlMinutes" runat="server"
                                            AutoPostBack="true" OnSelectedIndexChanged="DateOrTimeChanged"
                                            CssClass="form-select form-control-premium">
                                            <asp:ListItem Text="Min" Value="" />
                                            <asp:ListItem Text="00" Value="0" />
                                            <asp:ListItem Text="15" Value="15" />
                                            <asp:ListItem Text="30" Value="30" />
                                            <asp:ListItem Text="45" Value="45" />
                                        </asp:DropDownList>
                                    </div>
                                </div>

                                <div class="mb-0">
                                    <label class="text-label">Location <span class="text-danger">*</span></label>
                                    <asp:DropDownList runat="server" ID="ddlLocation" CssClass="form-select form-control-premium" />
                                </div>
                            </div>

                            <!-- RIGHT: Booked Meetings preview - always visible -->
                            <div class="col-md-9">
                                <label class="text-label">Booked / Rescheduled Meetings on Selected Date &amp; Type</label>
                                <div class="compact-preview rounded-3 p-2" style="min-height: 60px; max-height: 350px; overflow-y: auto; background: rgba(250, 250, 250, 0.75); backdrop-filter: blur(12px); border: 1px solid rgba(0,0,0,0.05); box-shadow: 0 4px 20px rgba(0, 0, 0, 0.04);">
                                    <asp:GridView runat="server" ID="gvBookedSlots"
                                        CssClass="table table-premium table-sm mb-0"
                                        AutoGenerateColumns="false"
                                        EmptyDataText="No meetings booked - select a date or VC type to load schedule."
                                        GridLines="None">
                                        <Columns>
                                            <asp:BoundField DataField="VCAccountName" HeaderText="Account" />
                                            <asp:BoundField DataField="Topic" HeaderText="Topic" />
                                            <asp:BoundField DataField="FromTime" HeaderText="From" DataFormatString="{0:HH:mm}" />
                                            <asp:BoundField DataField="ToTime" HeaderText="To" DataFormatString="{0:HH:mm}" />
                                            <asp:BoundField DataField="VCStatus" HeaderText="Status" />
                                        </Columns>
                                    </asp:GridView>
                                </div>
                            </div>
                        </div>

                        <!-- UNIT/FLOOR + VC DETAILS: Horizontal below schedule -->
                        <div class="row g-4 mb-5">
                            <div class="col-md-6">
                                <label class="text-label">Unit / Floor Details</label>
                                <asp:TextBox runat="server" ID="txtUnitFloor" CssClass="form-control form-control-premium" />
                            </div>
                            <div class="col-md-6">
                                <label class="text-label">VC Details (Notes)</label>
                                <asp:TextBox runat="server" ID="txtVCDetails" TextMode="MultiLine" Rows="2" CssClass="form-control form-control-premium" />
                            </div>
                        </div>

                        <!-- PARTICIPANTS -->
                        <h5 class="section-title mb-4">Participants <span class="text-danger">*</span></h5>
                        <div class="row g-3 mb-4">
                            <div class="col-md-8">
                                <asp:TextBox runat="server" ID="txtParticipant" CssClass="form-control form-control-premium" placeholder="Enter email address(es), comma-separated..." />
                            </div>
                            <div class="col-md-4">
                                <asp:Button runat="server" ID="btnParticipant" Text="Add Participant" OnClick="btnAddParticipant_Click" CssClass="btn btn-soft-primary w-100" />
                            </div>
                        </div>
                        <asp:Label runat="server" ID="lblParticipantMessage" CssClass="text-danger mb-3 d-block" />
                        <div class="table-responsive-premium mb-5">
                            <asp:GridView runat="server" ID="gvParticipants"
                                CssClass="table table-premium"
                                AutoGenerateColumns="false"
                                GridLines="None"
                                OnRowCommand="gvParticipants_RowCommand">
                                <Columns>
                                    <asp:BoundField DataField="ParticipantEmail" HeaderText="Participant Email" />
                                    <asp:TemplateField HeaderText="" ItemStyle-Width="60px" ItemStyle-CssClass="text-end">
                                        <ItemTemplate>
                                            <asp:LinkButton runat="server"
                                                CommandName="RemoveParticipant"
                                                CommandArgument='<%# Container.DataItemIndex %>'
                                                CssClass="btn-remove-participant"
                                                OnClientClick="return true;"
                                                ToolTip="Remove">
                                                <i class="bi bi-x-circle-fill"></i>
                                            </asp:LinkButton>
                                        </ItemTemplate>
                                    </asp:TemplateField>
                                </Columns>
                            </asp:GridView>
                        </div>

                        <div class="text-center">
                            <asp:Button runat="server" ID="btnFormSubmit" Text="Create VC Request"
                                OnClick="btnFormSubmit_Click" UseSubmitBehavior="true"
                                OnClientClick="showProcessingOverlay(); return true;"
                                CssClass="btn-dark-navy px-5" />
                        </div>

                    </div>
                </ContentTemplate>
                <Triggers>
                </Triggers>
            </asp:UpdatePanel>
        </div>
    </form>

    <!-- Processing Overlay -->
    <div id="processingOverlay" class="processing-overlay">
        <div class="rail-card premium-card text-center" style="max-width: 400px; position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); z-index: 10000;">
            <div class="spinner-border text-primary mb-4" style="width: 3rem; height: 3rem;" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <h5 class="fw-bold mb-2">Processing Your Request</h5>
            <p class="text-secondary mb-0">We're setting up your meeting. Please wait a moment.</p>
        </div>
    </div>

    <!-- Success Modal -->
    <div class="modal fade" id="successModal" tabindex="-1" data-bs-backdrop="static">
        <div class="modal-dialog modal-dialog-centered">
            <div class="modal-content modal-content-premium">
                <div class="modal-body modal-body-premium text-center py-5">
                    <div class="checkmark-container">
                        <svg class="checkmark" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 52 52">
                            <circle class="checkmark-circle" cx="26" cy="26" r="25" fill="none"/>
                            <path class="checkmark-check" fill="none" d="M14.1 27.2l7.1 7.2 16.7-16.8"/>
                        </svg>
                    </div>
                    <h3 class="fw-bold mb-3">Booking Confirmed!</h3>
                    <p class="text-secondary mb-4 px-4">Your VC request has been submitted successfully. Meeting invites have been sent to all participants.</p>
                    <div class="d-grid gap-2 col-8 mx-auto">
                        <a href="ViewRequests.aspx" class="btn-dark-navy text-decoration-none">View My Requests</a>
                        <a href="EmployeeDashboard.aspx" class="btn btn-light btn-premium">Back to Dashboard</a>
                    </div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
