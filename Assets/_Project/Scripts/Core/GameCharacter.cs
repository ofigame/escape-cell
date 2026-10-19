using BrickMuseum.Character;
using BrickMuseum.Data;
using BrickMuseum.UI;
using UnityEngine;

namespace BrickMuseum.Core
{
    /// <summary>The character workshop: pick hair, skin, hat, shirt, pattern, trousers and shoes; new items open with levels.</summary>
    public sealed partial class Game
    {
        private readonly CharacterUi chu = new CharacterUi();
        private PersonStudio studio;
        private Outfit editing;
        private int wardrobeTab;

        private void WireCharacter()
        {
            chu.Build(transform);
            studio = new PersonStudio(transform);
            chu.preview.texture = studio.texture;
            chu.previewDrag.Drag = e => studio.Turn(-e.delta.x * 0.45f);
            chu.doneBtn.onClick.AddListener(CloseCharacter);
            chu.randomBtn.onClick.AddListener(RandomOutfit);
            var slots = Wardrobe.Slots;
            for (int i = 0; i < slots.Count; i++) { int k = i; chu.AddTab(slots[i].title, () => ShowWardrobeTab(k)); }
            cu.characterBtn.onClick.AddListener(OpenCharacter);
        }

        private void OpenCharacter()
        {
            ClosePanel();
            editing = Progress.Outfit.Copy();
            studio.Show(editing);
            chu.canvas.gameObject.SetActive(true);
            ShowWardrobeTab(wardrobeTab);
        }

        private void CloseCharacter()
        {
            Progress.Outfit = editing;
            chu.canvas.gameObject.SetActive(false);
            Toast("Kıyafetin kaydedildi");
        }

        private void ShowWardrobeTab(int i)
        {
            wardrobeTab = i;
            chu.SelectTab(i);
            var slot = Wardrobe.Slots[i];
            chu.slotTitle.text = slot.title;
            chu.ClearOptions();
            string current = Wardrobe.Get(editing, slot.field);
            int level = Progress.Level;
            foreach (var item in slot.items)
            {
                var it = item;
                bool locked = level < it.level;
                chu.AddOption(it.label, it.IsColour ? Palette.Of(it.id) : (Color?)null, current == it.id, locked ? it.level : 0, () =>
                {
                    if (locked) { Toast($"{it.label} seviye {it.level}'de açılır"); return; }
                    Wardrobe.Set(editing, slot.field, it.id);
                    studio.Show(editing);
                    Sfx.Play("click", 0.7f);
                    ShowWardrobeTab(wardrobeTab);
                });
            }
        }

        /// <summary>A random outfit from the items already open.</summary>
        private void RandomOutfit()
        {
            int level = Progress.Level;
            foreach (var slot in Wardrobe.Slots)
            {
                var open = slot.items.FindAll(x => x.level <= level);
                Wardrobe.Set(editing, slot.field, open[Random.Range(0, open.Count)].id);
            }
            studio.Show(editing);
            Sfx.Play("rustle");
            ShowWardrobeTab(wardrobeTab);
        }
    }
}
