using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace XdcLocalDesktopAccessTool.App.Controls
{
    public partial class MnemonicPhraseGrid : UserControl
    {
        private const string MaskToken = "•••••";
        private int _currentWordCount = 12;

        public MnemonicPhraseGrid()
        {
            InitializeComponent();
            BuildGridStructure();
            SetWordCount(12);
        }

        private void BuildGridStructure()
        {
            MnemonicGrid.ColumnDefinitions.Clear();
            MnemonicGrid.RowDefinitions.Clear();

            // 4 equal star columns
            for (int i = 0; i < 4; i++)
                MnemonicGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 6 equal star rows (locked)
            for (int i = 0; i < 6; i++)
                MnemonicGrid.RowDefinitions.Add(
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        }

        private Grid CreateSlot(int index)
        {
            var slot = new Grid { Margin = new Thickness(6, 2, 6, 2) };

            slot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            slot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badge = new Border
            {
                Width = 26,
                Height = 23,
                CornerRadius = new CornerRadius(2),
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = System.Windows.Media.Brushes.Gainsboro,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            badge.Child = new TextBlock
            {
                Text = index.ToString(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var box = new TextBox
            {
                Height = 23,
                Padding = new Thickness(6, 1, 6, 1),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsReadOnly = true,
                Tag = index
            };

            Grid.SetColumn(box, 1);

            slot.Children.Add(badge);
            slot.Children.Add(box);

            return slot;
        }

        /// <summary>
        /// Locked spacing:
        /// - 24 words: rows 0-5 sequential (6x4)
        /// - 12 words: rows 0,2,4 only (gap rows 1,3,5 left empty)
        /// </summary>
        public void SetWordCount(int count)
        {
            _currentWordCount = count;
            MnemonicGrid.Children.Clear();

            if (count == 24)
            {
                int index = 1;
                for (int row = 0; row < 6; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        var slot = CreateSlot(index);
                        Grid.SetRow(slot, row);
                        Grid.SetColumn(slot, col);
                        MnemonicGrid.Children.Add(slot);
                        index++;
                    }
                }
            }
            else
            {
                // Row 0: 1-4
                for (int col = 0; col < 4; col++)
                {
                    int index = 1 + col;
                    var slot = CreateSlot(index);
                    Grid.SetRow(slot, 0);
                    Grid.SetColumn(slot, col);
                    MnemonicGrid.Children.Add(slot);
                }

                // Row 2: 5-8
                for (int col = 0; col < 4; col++)
                {
                    int index = 5 + col;
                    var slot = CreateSlot(index);
                    Grid.SetRow(slot, 2);
                    Grid.SetColumn(slot, col);
                    MnemonicGrid.Children.Add(slot);
                }

                // Row 4: 9-12
                for (int col = 0; col < 4; col++)
                {
                    int index = 9 + col;
                    var slot = CreateSlot(index);
                    Grid.SetRow(slot, 4);
                    Grid.SetColumn(slot, col);
                    MnemonicGrid.Children.Add(slot);
                }
            }
        }

        public void Populate(IReadOnlyList<string> words)
        {
            var boxes = GetTextBoxes();
            for (int i = 0; i < boxes.Count; i++)
                boxes[i].Text = i < words.Count ? words[i] : "";
        }

        public void Mask(int count)
        {
            var boxes = GetTextBoxes();
            for (int i = 0; i < boxes.Count; i++)
                boxes[i].Text = i < count ? MaskToken : "";
        }

        public void Clear()
        {
            foreach (var tb in GetTextBoxes())
                tb.Text = "";
        }

        public List<TextBox> GetTextBoxes()
        {
            return MnemonicGrid.Children
                .OfType<Grid>()
                .SelectMany(g => g.Children.OfType<TextBox>())
                .OrderBy(tb => (int)tb.Tag)
                .ToList();
        }
    }
}