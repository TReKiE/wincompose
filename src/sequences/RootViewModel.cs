//
//  WinCompose — a compose key for Windows — http://wincompose.info/
//
//  Copyright © 2013—2015 Sam Hocevar <sam@hocevar.net>
//              2014—2015 Benjamin Litzelmann
//
//  This program is free software. It comes without any warranty, to
//  the extent permitted by applicable law. You can redistribute it
//  and/or modify it under the terms of the Do What the Fuck You Want
//  to Public License, Version 2, as published by the WTFPL Task Force.
//  See http://www.wtfpl.net/ for more details.
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Data;
using WinCompose.Properties; // for UnicodeBlocks

namespace WinCompose
{
    public class RootViewModel : ViewModelBase
    {
        private string searchText;
        private SearchTokens searchTokens = new SearchTokens(null);
        private bool searchInSelection;

        public RootViewModel()
        {
            Settings.LoadConfig();
            Settings.LoadSequences();

            var categories = new List<CategoryViewModel>();
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public;
            foreach (var property in typeof(UnicodeBlocks).GetProperties(flags))
            {
                if (property.Name == "ResourceManager" || property.Name == "Culture")
                    continue;

                var name = (string)property.GetValue(null, null);
                var range = property.Name.Split(new[] { 'U' });
                var start = Convert.ToInt32(range[1], 16);
                var end = Convert.ToInt32(range[2], 16);
                categories.Add(new CategoryViewModel(name, start, end));
            }
            categories.Sort((x, y) => string.Compare(x.Name, y.Name, Thread.CurrentThread.CurrentCulture, CompareOptions.StringSort));

            var sortedCategories = new SortedList<int, CategoryViewModel>();
            foreach (var category in categories)
            {
                sortedCategories.Add(category.RangeEnd, category);
            }

            var sequences = new List<SequenceViewModel>();
            foreach (var desc in Settings.GetSequenceDescriptions())
            {
                // TODO: optimize me
                foreach (var category in sortedCategories)
                {
                    if (desc.Result.Length > 0 && category.Key > desc.Result[0])
                    {
                        sequences.Add(new SequenceViewModel(category.Value, desc));
                        break;
                    }
                }
            }

            var nonEmptyCategories = new List<CategoryViewModel>();
            foreach (var category in categories)
            {
                if (!category.IsEmpty)
                    nonEmptyCategories.Add(category);
            }
            Categories = nonEmptyCategories;

            Sequences = sequences;
            Instance = this;

            RefreshFilters();
        }

        public static RootViewModel Instance { get; private set; }

        public IEnumerable<CategoryViewModel> Categories { get; private set; }

        public IEnumerable<SequenceViewModel> Sequences { get; private set; }

        public string SearchText { get { return searchText; } set { SetValue(ref searchText, value, "SearchText", RefreshSearch); } }

        public bool SearchInSelection { get { return searchInSelection; } set { SetValue(ref searchInSelection, value, "SearchInSelection", x => RefreshFilters()); } }

        public void RefreshFilters()
        {
            var collectionView = CollectionViewSource.GetDefaultView(Sequences);
            collectionView.Filter = FilterFunc;
            collectionView.Refresh();
        }

        private void RefreshSearch(string text)
        {
            searchTokens = new SearchTokens(text);
            RefreshFilters();
        }

        private bool FilterFunc(object obj)
        {
            var sequence = (SequenceViewModel)obj;
            if (searchTokens.IsEmpty)
                return sequence.Category.IsSelected;

            if (SearchInSelection)
                return sequence.Category.IsSelected && sequence.Match(searchTokens);

            return sequence.Match(searchTokens);
        }
    }
}