using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

namespace Toolbox
{
    public sealed class ResultItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; private set; }
        public string Schedule { get; private set; }
        public string Status { get; private set; }
        public string Reason { get; private set; }
        public string Diagnostics { get; private set; }
        public bool HasDiagnostics { get { return !String.IsNullOrEmpty(Diagnostics); } }
        public Brush StatusForeground { get; private set; }
        public Brush StatusBackground { get; private set; }

        private readonly Action changed;
        private bool isChecked;
        internal Decision Decision { get; private set; }
        internal Slot Shift { get { return Decision == null ? null : Decision.Shift; } }
        public bool CanApply { get; private set; }
        public string SelectionLabel { get { return (Status == "Needs confirmation" ? "Confirm and select " : "Select ") + Name + " " + Schedule; } }
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                var next = value && CanApply;
                if (isChecked == next) return;
                isChecked = next;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("IsChecked"));
                changed();
            }
        }

        internal ResultItem(Decision decision, bool preview, Action changed)
        {
            this.changed = changed;
            Decision = decision;
            CanApply = decision != null && (decision.Status == "Eligible" || decision.Status == "Needs confirmation");
            isChecked = preview && decision != null && decision.Status == "Eligible";
            Status = decision == null ? "No shifts" : decision.IsApplied ? "Applied" :
                decision.Status == "Eligible" ? "Can apply" : decision.Status == "Assigned" ? "Approved" : decision.Status;
            Reason = decision == null ? "There are no shifts to show for the selected week." : decision.Reason;
            if (Status == "Needs confirmation") Reason += " Select to confirm for this shift.";
            if (decision != null)
            {
                Diagnostics = decision.Status == "Declined" ? decision.Shift.ApplicationDiagnostics : null;
                Name = decision.Shift.Name;
                Schedule = Planner.Local(decision.Shift.Start).ToString("ddd, MMM d") + " · " + WeekTimeline.TimeRange(decision.Shift) + " ET";
            }
            var review = new[] { "Needs confirmation", "Check Sling", "Declined", "Not sent" }.Contains(Status);
            StatusForeground = WeekTimeline.Brush(Status == "Applied" ? "#845B0D" : Status == "Approved" ? "#24539B" :
                Status == "Can apply" ? "#6A3D97" : review ? "#954D20" : "#455771");
            StatusBackground = WeekTimeline.Brush(Status == "Applied" ? "#FFF0CF" : Status == "Approved" ? "#E3EDFE" :
                Status == "Can apply" ? "#F0E7FA" : review ? "#FFF0E5" : "#EAF0F7");
        }
    }
}
