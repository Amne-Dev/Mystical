pragma Singleton
import QtQuick 2.15

QtObject {
    id: theme
    
    // Theme mode enumeration
    enum Mode {
        System,
        Light,
        Dark
    }
    
    // Current theme state
    property int currentMode: Theme.Mode.Dark
    property bool isDarkMode: currentMode === Theme.Mode.Dark || 
                              (currentMode === Theme.Mode.System && systemIsDark)
    property bool systemIsDark: true // Would be detected from system in real implementation
    
    // Windows 11 Fluent Design Colors - Dark Theme
    readonly property QtObject dark: QtObject {
        // Background colors
        readonly property color background: "#1e1e1e"
        readonly property color backgroundAlt: "#2d2d30"
        readonly property color surface: "#252526"
        readonly property color surfaceAlt: "#2f2f2f"
        readonly property color overlay: "#3c3c3c"
        
        // Accent colors
        readonly property color accent: "#0078d4"
        readonly property color accentHover: "#106ebe"
        readonly property color accentPressed: "#005a9e"
        readonly property color accentDisabled: "#4c4c4c"
        
        // Text colors
        readonly property color text: "#ffffff"
        readonly property color textSecondary: "#cccccc"
        readonly property color textTertiary: "#999999"
        readonly property color textDisabled: "#666666"
        readonly property color textOnAccent: "#ffffff"
        
        // Border colors
        readonly property color border: "#404040"
        readonly property color borderHover: "#606060"
        readonly property color borderFocus: "#0078d4"
        readonly property color borderDisabled: "#2d2d2d"
        
        // State colors
        readonly property color success: "#107c10"
        readonly property color warning: "#ff8c00"
        readonly property color error: "#d13438"
        readonly property color info: "#0078d4"
        
        // Interactive element colors
        readonly property color buttonBackground: "#404040"
        readonly property color buttonBackgroundHover: "#4a4a4a"
        readonly property color buttonBackgroundPressed: "#363636"
        readonly property color buttonBorder: "#606060"
        
        // Card and container colors
        readonly property color cardBackground: "#2d2d30"
        readonly property color cardBorder: "#404040"
        readonly property color cardHover: "#363639"
        
        // Input colors
        readonly property color inputBackground: "#2d2d30"
        readonly property color inputBorder: "#606060"
        readonly property color inputFocus: "#0078d4"
        readonly property color inputText: "#ffffff"
        readonly property color inputPlaceholder: "#999999"
        
        // Scrollbar colors
        readonly property color scrollbarBackground: "#2d2d30"
        readonly property color scrollbarThumb: "#606060"
        readonly property color scrollbarThumbHover: "#808080"
        
        // Glass/Acrylic effect colors
        readonly property color acrylicBackground: "#1e1e1e"
        readonly property color acrylicTint: "#2d2d30"
        readonly property color glassBackground: "#3c3c3c"
    }
    
    // Windows 11 Fluent Design Colors - Light Theme
    readonly property QtObject light: QtObject {
        // Background colors
        readonly property color background: "#f3f3f3"
        readonly property color backgroundAlt: "#ffffff"
        readonly property color surface: "#fafafa"
        readonly property color surfaceAlt: "#f6f6f6"
        readonly property color overlay: "#f0f0f0"
        
        // Accent colors
        readonly property color accent: "#0078d4"
        readonly property color accentHover: "#106ebe"
        readonly property color accentPressed: "#005a9e"
        readonly property color accentDisabled: "#cccccc"
        
        // Text colors
        readonly property color text: "#000000"
        readonly property color textSecondary: "#605e5c"
        readonly property color textTertiary: "#8a8886"
        readonly property color textDisabled: "#a19f9d"
        readonly property color textOnAccent: "#ffffff"
        
        // Border colors
        readonly property color border: "#d1d1d1"
        readonly property color borderHover: "#a19f9d"
        readonly property color borderFocus: "#0078d4"
        readonly property color borderDisabled: "#e1dfdd"
        
        // State colors
        readonly property color success: "#107c10"
        readonly property color warning: "#ff8c00"
        readonly property color error: "#d13438"
        readonly property color info: "#0078d4"
        
        // Interactive element colors
        readonly property color buttonBackground: "#ffffff"
        readonly property color buttonBackgroundHover: "#f3f2f1"
        readonly property color buttonBackgroundPressed: "#edebe9"
        readonly property color buttonBorder: "#d1d1d1"
        
        // Card and container colors
        readonly property color cardBackground: "#ffffff"
        readonly property color cardBorder: "#e1dfdd"
        readonly property color cardHover: "#f3f2f1"
        
        // Input colors
        readonly property color inputBackground: "#ffffff"
        readonly property color inputBorder: "#d1d1d1"
        readonly property color inputFocus: "#0078d4"
        readonly property color inputText: "#000000"
        readonly property color inputPlaceholder: "#8a8886"
        
        // Scrollbar colors
        readonly property color scrollbarBackground: "#f3f3f3"
        readonly property color scrollbarThumb: "#c8c6c4"
        readonly property color scrollbarThumbHover: "#a19f9d"
        
        // Glass/Acrylic effect colors
        readonly property color acrylicBackground: "#f3f3f3"
        readonly property color acrylicTint: "#ffffff"
        readonly property color glassBackground: "#fafafa"
    }
    
    // Current theme colors (automatically switches based on isDarkMode)
    readonly property QtObject colors: isDarkMode ? dark : light
    
    // Typography system
    readonly property QtObject typography: QtObject {
        // Font families
        readonly property string fontFamily: "Segoe UI Variable"
        readonly property string monoFontFamily: "Consolas"
        
        // Font sizes (following Windows 11 type ramp)
        readonly property int captionSize: 12
        readonly property int bodySize: 14
        readonly property int bodyLargeSize: 16
        readonly property int subtitleSize: 18
        readonly property int titleSize: 20
        readonly property int titleLargeSize: 24
        readonly property int displaySize: 28
        readonly property int headerSize: 32
        
        // Font weights
        readonly property int light: Font.Light
        readonly property int normal: Font.Normal
        readonly property int medium: Font.Medium
        readonly property int semiBold: Font.DemiBold
        readonly property int bold: Font.Bold
        
        // Line heights (as multipliers)
        readonly property real captionLineHeight: 1.3
        readonly property real bodyLineHeight: 1.4
        readonly property real titleLineHeight: 1.2
        readonly property real headerLineHeight: 1.1
    }
    
    // Spacing system (8px base unit)
    readonly property QtObject spacing: QtObject {
        readonly property int none: 0
        readonly property int xs: 4      // 0.5 * base
        readonly property int sm: 8      // 1 * base
        readonly property int md: 16     // 2 * base
        readonly property int lg: 24     // 3 * base
        readonly property int xl: 32     // 4 * base
        readonly property int xxl: 48    // 6 * base
        readonly property int xxxl: 64   // 8 * base
        
        // Component-specific spacing
        readonly property int cardPadding: 16
        readonly property int buttonPadding: 12
        readonly property int inputPadding: 8
        readonly property int containerMargin: 20
    }
    
    // Border radius system
    readonly property QtObject radius: QtObject {
        readonly property int none: 0
        readonly property int xs: 2
        readonly property int sm: 4
        readonly property int md: 6
        readonly property int lg: 8
        readonly property int xl: 12
        readonly property int pill: 999  // For pill-shaped elements
        
        // Component-specific radius
        readonly property int button: 4
        readonly property int card: 8
        readonly property int input: 4
        readonly property int overlay: 8
    }
    
    // Shadow system
    readonly property QtObject shadows: QtObject {
        readonly property QtObject elevation2: QtObject {
            readonly property color color: isDarkMode ? Qt.rgba(0, 0, 0, 0.4) : Qt.rgba(0, 0, 0, 0.1)
            readonly property real blur: 4
            readonly property real offsetX: 0
            readonly property real offsetY: 2
        }
        
        readonly property QtObject elevation4: QtObject {
            readonly property color color: isDarkMode ? Qt.rgba(0, 0, 0, 0.5) : Qt.rgba(0, 0, 0, 0.15)
            readonly property real blur: 8
            readonly property real offsetX: 0  
            readonly property real offsetY: 4
        }
        
        readonly property QtObject elevation8: QtObject {
            readonly property color color: isDarkMode ? Qt.rgba(0, 0, 0, 0.6) : Qt.rgba(0, 0, 0, 0.2)
            readonly property real blur: 16
            readonly property real offsetX: 0
            readonly property real offsetY: 8
        }
        
        readonly property QtObject elevation16: QtObject {
            readonly property color color: isDarkMode ? Qt.rgba(0, 0, 0, 0.7) : Qt.rgba(0, 0, 0, 0.25)
            readonly property real blur: 32
            readonly property real offsetX: 0
            readonly property real offsetY: 16
        }
    }
    
    // Animation durations and easing
    readonly property QtObject animation: QtObject {
        // Duration constants (in milliseconds)
        readonly property int fast: 150
        readonly property int normal: 250
        readonly property int slow: 400
        readonly property int slower: 600
        
        // Component-specific durations
        readonly property int hover: 150
        readonly property int focus: 200
        readonly property int press: 100
        readonly property int modal: 300
        readonly property int pageTransition: 400
        
        // Easing curves
        readonly property var easeOut: Easing.OutQuart
        readonly property var easeIn: Easing.InQuart
        readonly property var easeInOut: Easing.InOutQuart
        readonly property var easeBack: Easing.OutBack
        readonly property var easeBounce: Easing.OutBounce
        readonly property var easeElastic: Easing.OutElastic
    }
    
    // Z-index layering system
    readonly property QtObject zIndex: QtObject {
        readonly property int background: -1
        readonly property int base: 0
        readonly property int content: 1
        readonly property int elevated: 10
        readonly property int dropdown: 100
        readonly property int sticky: 200
        readonly property int overlay: 1000
        readonly property int modal: 2000
        readonly property int popover: 3000
        readonly property int tooltip: 4000
        readonly property int notification: 5000
    }
    
    // Opacity levels
    readonly property QtObject opacity: QtObject {
        readonly property real disabled: 0.5
        readonly property real ghost: 0.6
        readonly property real muted: 0.7
        readonly property real normal: 1.0
        
        // Overlay opacities
        readonly property real backdropLight: 0.3
        readonly property real backdropDark: 0.6
        readonly property real glassEffect: 0.8
    }
    
    // Breakpoints for responsive design
    readonly property QtObject breakpoints: QtObject {
        readonly property int xs: 480
        readonly property int sm: 768
        readonly property int md: 1024
        readonly property int lg: 1280
        readonly property int xl: 1536
    }
    
    // Component-specific theme objects
    readonly property QtObject button: QtObject {
        readonly property color background: colors.buttonBackground
        readonly property color backgroundHover: colors.buttonBackgroundHover
        readonly property color backgroundPressed: colors.buttonBackgroundPressed
        readonly property color border: colors.buttonBorder
        readonly property color text: colors.text
        readonly property int radius: theme.radius.button
        readonly property int padding: spacing.buttonPadding
    }
    
    readonly property QtObject card: QtObject {
        readonly property color background: colors.cardBackground
        readonly property color backgroundHover: colors.cardHover
        readonly property color border: colors.cardBorder
        readonly property int radius: theme.radius.card
        readonly property int padding: spacing.cardPadding
    }
    
    readonly property QtObject input: QtObject {
        readonly property color background: colors.inputBackground
        readonly property color border: colors.inputBorder
        readonly property color borderFocus: colors.inputFocus
        readonly property color text: colors.inputText
        readonly property color placeholder: colors.inputPlaceholder
        readonly property int radius: theme.radius.input
        readonly property int padding: spacing.inputPadding
    }
    
    // Utility functions
    function rgba(r, g, b, a) {
        return Qt.rgba(r / 255, g / 255, b / 255, a)
    }
    
    function hsla(h, s, l, a) {
        return Qt.hsla(h / 360, s / 100, l / 100, a)
    }
    
    function adjustAlpha(color, alpha) {
        return Qt.rgba(color.r, color.g, color.b, alpha)
    }
    
    function lighten(color, amount) {
        return Qt.lighter(color, 1 + amount)
    }
    
    function darken(color, amount) {
        return Qt.darker(color, 1 + amount)
    }
    
    // Theme switching functions
    function setTheme(mode) {
        currentMode = mode
    }
    
    function toggleTheme() {
        if (currentMode === Theme.Mode.Light) {
            setTheme(Theme.Mode.Dark)
        } else {
            setTheme(Theme.Mode.Light)
        }
    }
    
    // System theme detection (would be implemented in C++ in real app)
    function detectSystemTheme() {
        // This would query the system theme in a real implementation
        return Theme.Mode.Dark
    }
    
    Component.onCompleted: {
        if (currentMode === Theme.Mode.System) {
            systemIsDark = detectSystemTheme() === Theme.Mode.Dark
        }
    }
}