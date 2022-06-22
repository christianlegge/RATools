﻿using Jamiras.Commands;
using Jamiras.Components;
using Jamiras.DataModels;
using Jamiras.ViewModels;
using Jamiras.ViewModels.Fields;
using RATools.Data;
using RATools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace RATools.ViewModels
{
    [DebuggerDisplay("{Title} ({ViewerType,nq})")]
    public abstract class AssetViewModelBase : ViewerViewModelBase
    {
        public AssetViewModelBase(GameViewModel owner)
            : base(owner)
        {
            Generated = new AssetSourceViewModel(this, "Generated");
            Local = new AssetSourceViewModel(this, "Local");
            Published = new AssetSourceViewModel(this, "Published");

            if (owner == null || String.IsNullOrEmpty(owner.RACacheDirectory))
            {
                UpdateLocalCommand = DisabledCommand.Instance;
                DeleteLocalCommand = DisabledCommand.Instance;
            }
            else
            {
                UpdateLocalCommand = new DelegateCommand(UpdateLocal);
                DeleteLocalCommand = new DelegateCommand(DeleteLocal);
            }
        }

        /// <summary>
        /// The asset generated by the script.
        /// </summary>
        public AssetSourceViewModel Generated { get; private set; }
        /// <summary>
        /// The asset as saved on disk.
        /// </summary>
        public AssetSourceViewModel Local { get; private set; }
        /// <summary>
        /// The asset as saved on the server.
        /// </summary>
        public AssetSourceViewModel Published { get; private set; }

        /// <summary>
        /// The asset to compare with the generated asset.
        /// </summary>
        /// <remarks>
        /// References <see cref="Local"/>, <see cref="Published"/>, or null.
        /// </remarks>
        public AssetSourceViewModel Other 
        { 
            get { return _other; }
            private set
            {
                if (_other != value)
                {
                    _other = value;
                    OnPropertyChanged(() => Other);
                }
            }
        }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private AssetSourceViewModel _other;

        public CommandBase DeleteLocalCommand { get; protected set; }

        /// <summary>
        /// True if this asset includes a <see cref="Generated"/> aspect.
        /// </summary>
        public virtual bool IsGenerated
        {
            get { return Generated.Asset != null; }
        }

        internal bool AllocateLocalId(int value)
        {
            // don't attempt to assign a temporary ID to a published asset
            if (Published.Asset != null)
                return false;

            var localId = Local.Id;
            var generatedId = Generated.Id;

            // if either asset provides an ID, copy it into the other
            if (localId != generatedId)
            {
                if (localId == 0 && Local.Asset != null)
                {
                    Local.Asset.Id = generatedId;
                    Local.Asset = Local.Asset; // refresh the viewmodel's ID property
                }

                if (generatedId == 0 && Generated.Asset != null)
                {
                    Generated.Asset.Id = localId;
                    Generated.Asset = Generated.Asset; // refresh the viewmodel's ID property
                }

                return false;
            }

            if (localId != 0)
            {
                // both assets have a valid id. do nothing
                return false;
            }

            var localAsset = Local.Asset;
            var generatedAsset = Generated.Asset;

            if (localAsset == null && generatedAsset == null)
            {
                // this should never happen as it's only possible if a published asset exists
                return false;
            }

            // assign the local id to both assets
            if (localAsset != null)
            {
                localAsset.Id = value;
                Local.Asset = localAsset; // refresh the viewmodel's ID property
            }

            if (generatedAsset != null)
            {
                generatedAsset.Id = value;
                Generated.Asset = generatedAsset; // refresh the viewmodel's ID property
            }

            return true;
        }

        public static readonly ModelProperty IdProperty = ModelProperty.Register(typeof(AssetViewModelBase), "Id", typeof(int), 0);
        public int Id
        {
            get { return (int)GetValue(IdProperty); }
            protected set { SetValue(IdProperty, value); }
        }

        public static readonly ModelProperty PointsProperty = ModelProperty.Register(typeof(AssetViewModelBase), "Points", typeof(int), 0);
        public int Points
        {
            get { return (int)GetValue(PointsProperty); }
            protected set { SetValue(PointsProperty, value); }
        }


        public static readonly ModelProperty IsPointsModifiedProperty = ModelProperty.Register(typeof(AssetViewModelBase), "IsPointsModified", typeof(bool), false);
        public bool IsPointsModified
        {
            get { return (bool)GetValue(IsPointsModifiedProperty); }
            protected set { SetValue(IsPointsModifiedProperty, value); }
        }

        private void UpdateModified()
        {
            var coreAsset = Published.Asset;

            if (!IsGenerated)
            {
                ModificationMessage = null;
                CanUpdate = false;

                Other = null;
                IsTitleModified = false;
                IsDescriptionModified = false;
                IsPointsModified = false;
                CompareState = GeneratedCompareState.None;

                if (coreAsset != null)
                {
                    Triggers = Published.TriggerList;
                    if (coreAsset.IsUnofficial)
                        TriggerSource = "Unofficial (Not Generated)";
                    else
                        TriggerSource = "Core (Not Generated)";
                }
                else if (Local.Asset != null)
                {
                    Triggers = Local.TriggerList;
                    TriggerSource = "Local (Not Generated)";
                }
            }
            else if (IsModified(Local, true))
            {
                if (coreAsset != null && !IsModified(Published, false))
                {
                    if (coreAsset.IsUnofficial)
                        TriggerSource = "Generated (Same as Unofficial)";
                    else
                        TriggerSource = "Generated (Same as Core)";
                }
                else
                {
                    TriggerSource = "Generated";
                }

                Other = Local;
                ModificationMessage = "Local differs from generated";
                CompareState = GeneratedCompareState.LocalDiffers;
                CanUpdate = true;
            }
            else if (coreAsset != null && IsModified(Published, true))
            {
                if (Local.Asset != null)
                {
                    TriggerSource = "Generated (Same as Local)";
                    CanUpdate = false;
                }
                else
                {
                    TriggerSource = "Generated (Not in Local)";
                    CanUpdate = true;
                }

                if (coreAsset.IsUnofficial)
                    ModificationMessage = "Unofficial differs from generated";
                else
                    ModificationMessage = "Core differs from generated";

                Other = Published;
                CompareState = GeneratedCompareState.PublishedDiffers;
            }
            else
            {
                if (Local.Asset == null && IsGenerated)
                {
                    if (coreAsset == null)
                        TriggerSource = "Generated (Not in Local)";
                    else if (coreAsset.IsUnofficial)
                        TriggerSource = "Generated (Same as Unofficial, not in Local)";
                    else
                        TriggerSource = "Generated (Same as Core, not in Local)";

                    ModificationMessage = "Local " + ViewerType + " does not exist";
                    CompareState = GeneratedCompareState.PublishedMatchesNotLocal;
                    CanUpdate = true;
                    Other = null;
                }
                else
                {
                    if (coreAsset == null)
                        TriggerSource = "Generated (Same as Local)";
                    else if (coreAsset.IsUnofficial)
                        TriggerSource = "Generated (Same as Unofficial and Local)";
                    else
                        TriggerSource = "Generated (Same as Core and Local)";

                    ModificationMessage = null;
                    CompareState = GeneratedCompareState.Same;
                    CanUpdate = false;
                    Other = null;
                }

                IsTitleModified = false;
                IsDescriptionModified = false;
                IsPointsModified = false;

                Triggers = Generated.TriggerList;
            }
        }

        protected bool IsModified(AssetSourceViewModel assetViewModel, bool updateTriggers)
        {
            if (assetViewModel.Asset == null)
                return false;

            bool isModified = false;
            if (assetViewModel.Title.Text != Generated.Title.Text)
                IsTitleModified = isModified = true;
            if (assetViewModel.Description.Text != Generated.Description.Text)
                IsDescriptionModified = isModified = true;

            isModified |= AreAssetSpecificPropertiesModified(assetViewModel, Generated);

            var compareTriggers = new List<TriggerViewModel>(assetViewModel.TriggerList);
            var triggers = new List<TriggerViewModel>();
            var numberFormat = ServiceRepository.Instance.FindService<ISettings>().HexValues ? NumberFormat.Hexadecimal : NumberFormat.Decimal;
            var emptyTrigger = new TriggerViewModel("", (Achievement)null, numberFormat, null);

            foreach (var trigger in Generated.TriggerList)
            {
                var compareTrigger = compareTriggers.FirstOrDefault(t => t.Label == trigger.Label);
                if (compareTrigger != null)
                    compareTriggers.Remove(compareTrigger);
                else
                    compareTrigger = emptyTrigger;

                triggers.Add(new TriggerComparisonViewModel(trigger, compareTrigger, numberFormat, _owner.Notes)
                {
                    CopyToClipboardCommand = trigger.CopyToClipboardCommand
                });
            }

            foreach (var compareTrigger in compareTriggers)
                triggers.Add(new TriggerComparisonViewModel(emptyTrigger, compareTrigger, numberFormat, _owner.Notes));

            if (updateTriggers)
                Triggers = triggers;

            return isModified || 
                triggers.Any(t => t.Groups.Any(g => g.Requirements.OfType<RequirementComparisonViewModel>().Any(r => r.IsModified)));
        }

        protected virtual bool AreAssetSpecificPropertiesModified(AssetSourceViewModel left, AssetSourceViewModel right)
        {
            return false;
        }

        public int SourceLine
        {
            get { return (Generated.Asset != null) ? Generated.Asset.SourceLine : 0; }
        }

        private void UpdateLocal()
        {
            StringBuilder warning = new StringBuilder();
            UpdateLocal(warning, false);

            if (warning.Length > 0)
                TaskDialogViewModel.ShowWarningMessage("Your " + ViewerType + " may not function as expected.", warning.ToString());
        }

        internal void UpdateLocal(StringBuilder warning, bool validateAll)
        {
            var asset = Generated.Asset;
            if (asset.Id == 0)
                asset.Id = Id;

            if (String.IsNullOrEmpty(asset.BadgeName) || asset.BadgeName == "0")
                asset.BadgeName = BadgeName;

            UpdateLocal(asset, Local.Asset, warning, validateAll);

            Local = new AssetSourceViewModel(this, "Local");
            Local.Asset = Generated.Asset;

            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        protected abstract void UpdateLocal(AssetBase asset, AssetBase localAsset, StringBuilder warning, bool validateAll);

        private void DeleteLocal()
        {
            UpdateLocal(null, Local.Asset, null, false);

            Local = new AssetSourceViewModel(this, "Local");
            OnPropertyChanged(() => Local);
            UpdateModified();
        }

        internal virtual void OnShowHexValuesChanged(ModelPropertyChangedEventArgs e)
        {
            foreach (var trigger in Triggers)
            {
                foreach (var group in trigger.Groups)
                    group.OnShowHexValuesChanged(e);
            }

            Generated.OnShowHexValuesChanged(e);
            Local.OnShowHexValuesChanged(e);
            Published.OnShowHexValuesChanged(e);
        }

        public static readonly ModelProperty BadgeProperty = ModelProperty.RegisterDependant(typeof(AssetViewModelBase), "Badge", typeof(ImageSource), new ModelProperty[0], GetBadge);
        public ImageSource Badge
        {
            get { return (ImageSource)GetValue(BadgeProperty); }
        }

        internal string BadgeName { get; set; }

        private static ImageSource GetBadge(ModelBase model)
        {
            var vm = (AssetViewModelBase)model;
            if (!String.IsNullOrEmpty(vm.Published.BadgeName))
                return vm.Published.Badge;
            if (!String.IsNullOrEmpty(vm.Local.BadgeName))
                return vm.Local.Badge;

            if (!String.IsNullOrEmpty(vm.BadgeName))
            {
                vm.Local.BadgeName = vm.BadgeName;
                return vm.Local.Badge;
            }

            return null;
        }

        private static bool IsValidBadgeName(string badgeName)
        {
            if (String.IsNullOrEmpty(badgeName))
                return false;
            if (badgeName == "0")
                return false;
            if (badgeName == "00000")
                return false;

            return true;
        }

        public override void Refresh()
        {
            var generatedAsset = Generated.Asset;
            var localAsset = Local.Asset;
            var coreAsset = Published.Asset;

            Published.Source = (coreAsset == null) ? "Published" :
                coreAsset.IsUnofficial ? "Published (Unofficial)" : "Published (Core)";

            if (generatedAsset != null)
                BindViewModel(Generated);
            else if (coreAsset != null)
                LoadViewModel(Published);
            else if (localAsset != null)
                LoadViewModel(Local);

            if (Generated.Id != 0)
                Id = Generated.Id;
            else if (Local.Id > 111000000 && Published.Id != 0)
                Id = Published.Id;
            else if (Local.Id != 0)
                Id = Local.Id;
            else
                Id = Published.Id;

            if (IsValidBadgeName(Generated.BadgeName))
                BadgeName = Generated.BadgeName;
            else if (IsValidBadgeName(Local.BadgeName))
                BadgeName = Local.BadgeName;
            else if (IsValidBadgeName(Published.BadgeName))
                BadgeName = Published.BadgeName;
            else
                BadgeName = null;

            UpdateModified();

            base.Refresh();
        }

        private void BindViewModel(AssetSourceViewModel viewModel)
        {
            SetBinding(TitleProperty, new ModelBinding(viewModel.Title, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
            SetBinding(DescriptionProperty, new ModelBinding(viewModel.Description, TextFieldViewModel.TextProperty, ModelBindingMode.OneWay));
            SetBinding(PointsProperty, new ModelBinding(viewModel.Points, IntegerFieldViewModel.ValueProperty, ModelBindingMode.OneWay));
        }

        private void LoadViewModel(AssetSourceViewModel viewModel)
        {
            Title = viewModel.Title.Text;
            Description = viewModel.Description.Text;
            Points = viewModel.Points.Value.GetValueOrDefault();
        }

        public static readonly ModelProperty TriggerSourceProperty =
            ModelProperty.Register(typeof(AssetViewModelBase), "TriggerSource", typeof(string), "Generated");

        public string TriggerSource
        {
            get { return (string)GetValue(TriggerSourceProperty); }
            private set { SetValue(TriggerSourceProperty, value); }
        }

        public static readonly ModelProperty TriggersProperty = ModelProperty.Register(typeof(AssetViewModelBase),
            "Triggers", typeof(IEnumerable<TriggerViewModel>), new TriggerViewModel[0]);

        public IEnumerable<TriggerViewModel> Triggers
        {
            get { return (IEnumerable<TriggerViewModel>)GetValue(TriggersProperty); }
            private set { SetValue(TriggersProperty, value); }
        }

        internal abstract IEnumerable<TriggerViewModel> BuildTriggerList(AssetSourceViewModel assetViewModel);
    }
}
