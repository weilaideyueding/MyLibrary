        void AllTheTEXTURES(ref GUIStyle s, Texture2D t)
        {
            s.hover.background = s.onHover.background = s.focused.background = s.onFocused.background =
                s.active.background = s.onActive.background = s.normal.background = s.onNormal.background = t;
            s.hover.scaledBackgrounds = s.onHover.scaledBackgrounds = s.focused.scaledBackgrounds =
                s.onFocused.scaledBackgrounds = s.active.scaledBackgrounds = s.onActive.scaledBackgrounds =
                    s.normal.scaledBackgrounds = s.onNormal.scaledBackgrounds = new Texture2D[] {t};
        }
