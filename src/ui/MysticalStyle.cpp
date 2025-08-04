#include "MysticalStyle.h"
#include <QProxyStyle>
#include <QStyleOption>
#include <QPainter>
#include <QApplication>
#include <QPalette>
#include <QWidget>
#include <QStyle>
#include <QStyleOptionButton>
#include <QStyleOptionSlider>
#include <QStyleOptionProgressBar>
#include <QStyleOptionFrame>
#include <QStyleOptionTab>
#include <QSettings>
#include <QOperatingSystemVersion>

#ifdef Q_OS_WIN
#include <windows.h>
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")
#endif


MysticalStyle::MysticalStyle(QStyle* baseStyle)
    : QProxyStyle(baseStyle)
{
    setObjectName("MysticalStyle");
}

void MysticalStyle::drawPrimitive(PrimitiveElement element, const QStyleOption* option,
                                  QPainter* painter, const QWidget* widget) const
{
    switch (element) {
    case PE_PanelButtonCommand:
    case PE_PanelButtonTool:
        drawWindows11Button(option, painter, widget);
        return;
        
    case PE_IndicatorScrollBarAddLine:
    case PE_IndicatorScrollBarSubLine:
    case PE_IndicatorScrollBarSlider:
        // Handle in drawComplexControl
        break;
        
    case PE_FrameLineEdit:
        drawWindows11LineEdit(option, painter, widget);
        return;
        
    case PE_FrameFocusRect:
        // Draw Windows 11 style focus ring
        if (option->state & State_KeyboardFocusChange) {
            painter->save();
            painter->setRenderHint(QPainter::Antialiasing);
            QPen pen(getAccentColor(), FocusRingWidth);
            painter->setPen(pen);
            painter->setBrush(Qt::NoBrush);
            painter->drawRoundedRect(option->rect.adjusted(1, 1, -1, -1), CornerRadius, CornerRadius);
            painter->restore();
        }
        return;
        
    default:
        break;
    }
    
    QProxyStyle::drawPrimitive(element, option, painter, widget);
}

void MysticalStyle::drawControl(ControlElement element, const QStyleOption* option,
                                QPainter* painter, const QWidget* widget) const
{
    switch (element) {
    case CE_PushButton:
    case CE_PushButtonBevel:
        drawWindows11Button(option, painter, widget);
        return;
        
    case CE_ProgressBar:
        if (const QStyleOptionProgressBar* progressOption = 
            qstyleoption_cast<const QStyleOptionProgressBar*>(option)) {
            drawWindows11ProgressBar(progressOption, painter, widget);
            return;
        }
        break;
        
    case CE_TabBarTab:
        drawWindows11TabBar(option, painter, widget);
        return;
        
    default:
        break;
    }
    
    QProxyStyle::drawControl(element, option, painter, widget);
}

void MysticalStyle::drawComplexControl(ComplexControl control, const QStyleOptionComplex* option,
                                       QPainter* painter, const QWidget* widget) const
{
    switch (control) {
    case CC_ScrollBar:
        if (const QStyleOptionSlider* scrollBarOption = 
            qstyleoption_cast<const QStyleOptionSlider*>(option)) {
            drawWindows11ScrollBar(scrollBarOption, painter, widget);
            return;
        }
        break;
        
    default:
        break;
    }
    
    QProxyStyle::drawComplexControl(control, option, painter, widget);
}

int MysticalStyle::pixelMetric(PixelMetric metric, const QStyleOption* option,
                               const QWidget* widget) const
{
    switch (metric) {
    case PM_ButtonMargin:
        return ButtonPadding;
        
    case PM_ScrollBarExtent:
        return ScrollBarWidth;
        
    case PM_ScrollBarSliderMin:
        return 20;
        
    case PM_TabBarTabHSpace:
        return 20;
        
    case PM_TabBarTabVSpace:
        return 8;
        
    case PM_TabBarBaseHeight:
        return 2;
        
    case PM_FocusFrameHMargin:
    case PM_FocusFrameVMargin:
        return FocusRingWidth;
        
    default:
        break;
    }
    
    return QProxyStyle::pixelMetric(metric, option, widget);
}

QSize MysticalStyle::sizeFromContents(ContentsType type, const QStyleOption* option,
                                      const QSize& size, const QWidget* widget) const
{
    QSize newSize = QProxyStyle::sizeFromContents(type, option, size, widget);
    
    switch (type) {
    case CT_PushButton:
        newSize.setHeight(qMax(newSize.height(), 32));
        newSize.rwidth() += ButtonPadding * 2;
        break;
        
    case CT_TabBarTab:
        newSize.setHeight(TabBarHeight);
        break;
        
    default:
        break;
    }
    
    return newSize;
}

QRect MysticalStyle::subElementRect(SubElement element, const QStyleOption* option,
                                   const QWidget* widget) const
{
    return QProxyStyle::subElementRect(element, option, widget);
}

QRect MysticalStyle::subControlRect(ComplexControl control, const QStyleOptionComplex* option,
                                   SubControl subControl, const QWidget* widget) const
{
    return QProxyStyle::subControlRect(control, option, subControl, widget);
}

int MysticalStyle::styleHint(StyleHint hint, const QStyleOption* option,
                            const QWidget* widget, QStyleHintReturn* returnData) const
{
    switch (hint) {
    case SH_Button_FocusPolicy:
        return Qt::StrongFocus;
        
    case SH_ScrollBar_Transient:
        return true; // Auto-hide scrollbars like Windows 11
        
    case SH_ScrollBar_ContextMenu:
        return false;
        
    default:
        break;
    }
    
    return QProxyStyle::styleHint(hint, option, widget, returnData);
}

QPixmap MysticalStyle::standardPixmap(StandardPixmap standardPixmap, const QStyleOption* option,
                                     const QWidget* widget) const
{
    return QProxyStyle::standardPixmap(standardPixmap, option, widget);
}

QPalette MysticalStyle::standardPalette() const
{
    QPalette palette = QProxyStyle::standardPalette();
    
    bool isDark = isDarkMode();
    QColor accent = getAccentColor();
    QColor background = getBackgroundColor(isDark);
    QColor foreground = getForegroundColor(isDark);
    
    // Window colors
    palette.setColor(QPalette::Window, background);
    palette.setColor(QPalette::WindowText, foreground);
    
    // Button colors
    palette.setColor(QPalette::Button, background.lighter(110));
    palette.setColor(QPalette::ButtonText, foreground);
    
    // Highlight colors
    palette.setColor(QPalette::Highlight, accent);
    palette.setColor(QPalette::HighlightedText, Qt::white);
    
    // Base colors for input fields
    palette.setColor(QPalette::Base, isDark ? QColor(45, 45, 45) : Qt::white);
    palette.setColor(QPalette::Text, foreground);
    
    return palette;
}

// Private implementation methods

void MysticalStyle::drawWindows11Button(const QStyleOption* option, QPainter* painter, const QWidget* widget) const
{
    Q_UNUSED(widget)
    
    painter->save();
    painter->setRenderHint(QPainter::Antialiasing);
    
    QRect rect = option->rect;
    QColor buttonColor = getBackgroundColor(isDarkMode()).lighter(110);
    
    if (option->state & State_MouseOver) {
        buttonColor = getHoverColor(buttonColor);
    }
    if (option->state & State_Sunken) {
        buttonColor = getPressedColor(buttonColor);
    }
    
    drawRoundedRect(painter, rect, CornerRadius, buttonColor);
    
    // Draw focus ring if needed
    if (option->state & State_HasFocus && option->state & State_KeyboardFocusChange) {
        QPen pen(getAccentColor(), FocusRingWidth);
        painter->setPen(pen);
        painter->setBrush(Qt::NoBrush);
        painter->drawRoundedRect(rect.adjusted(1, 1, -1, -1), CornerRadius, CornerRadius);
    }
    
    painter->restore();
}

void MysticalStyle::drawWindows11ScrollBar(const QStyleOptionSlider* option, QPainter* painter, const QWidget* widget) const
{
    Q_UNUSED(widget)
    
    painter->save();
    painter->setRenderHint(QPainter::Antialiasing);
    
    QRect rect = option->rect;
    bool isDark = isDarkMode();
    
    // Draw track
    QColor trackColor = isDark ? QColor(60, 60, 60) : QColor(240, 240, 240);
    painter->fillRect(rect, trackColor);
    
    // Draw slider
    QRect sliderRect = subControlRect(CC_ScrollBar, option, SC_ScrollBarSlider, widget);
    if (sliderRect.isValid()) {
        QColor sliderColor = isDark ? QColor(120, 120, 120) : QColor(180, 180, 180);
        
        if (option->state & State_MouseOver) {
            sliderColor = getHoverColor(sliderColor);
        }
        if (option->state & State_Sunken) {
            sliderColor = getPressedColor(sliderColor);
        }
        
        drawRoundedRect(painter, sliderRect, 6, sliderColor);
    }
    
    painter->restore();
}

void MysticalStyle::drawWindows11TabBar(const QStyleOption* option, QPainter* painter, const QWidget* widget) const
{
    Q_UNUSED(widget)
    
    if (const QStyleOptionTab* tabOption = qstyleoption_cast<const QStyleOptionTab*>(option)) {
        painter->save();
        painter->setRenderHint(QPainter::Antialiasing);
        
        QRect rect = option->rect;
        bool isSelected = tabOption->state & State_Selected;
        bool isHovered = tabOption->state & State_MouseOver;
        
        QColor tabColor = getBackgroundColor(isDarkMode());
        if (isSelected) {
            tabColor = getAccentColor();
        } else if (isHovered) {
            tabColor = getHoverColor(tabColor);
        }
        
        // Draw tab background
        if (isSelected || isHovered) {
            drawRoundedRect(painter, rect.adjusted(4, 4, -4, -4), CornerRadius, tabColor);
        }
        
        // Draw selection indicator
        if (isSelected) {
            QRect indicator = rect;
            indicator.setHeight(3);
            indicator.moveBottom(rect.bottom());
            indicator.adjust(8, 0, -8, 0);
            drawRoundedRect(painter, indicator, 1, getAccentColor());
        }
        
        painter->restore();
    }
}

void MysticalStyle::drawWindows11ProgressBar(const QStyleOptionProgressBar* option, QPainter* painter, const QWidget* widget) const
{
    Q_UNUSED(widget)
    
    painter->save();
    painter->setRenderHint(QPainter::Antialiasing);
    
    QRect rect = option->rect;
    
    // Draw background
    QColor backgroundColor = isDarkMode() ? QColor(60, 60, 60) : QColor(230, 230, 230);
    drawRoundedRect(painter, rect, CornerRadius, backgroundColor);
    
    // Draw progress
    if (option->progress > option->minimum) {
        double progress = static_cast<double>(option->progress - option->minimum) / 
                         (option->maximum - option->minimum);
        
        QRect progressRect = rect;
        progressRect.setWidth(static_cast<int>(rect.width() * progress));
        
        drawRoundedRect(painter, progressRect, CornerRadius, getAccentColor());
    }
    
    painter->restore();
}

void MysticalStyle::drawWindows11LineEdit(const QStyleOption* option, QPainter* painter, const QWidget* widget) const
{
    Q_UNUSED(widget)
    
    painter->save();
    painter->setRenderHint(QPainter::Antialiasing);
    
    QRect rect = option->rect;
    bool hasFocus = option->state & State_HasFocus;
    bool isEnabled = option->state & State_Enabled;
    
    // Draw background
    QColor backgroundColor = isDarkMode() ? QColor(45, 45, 45) : Qt::white;
    if (!isEnabled) {
        backgroundColor = backgroundColor.darker(110);
    }
    
    drawRoundedRect(painter, rect, CornerRadius, backgroundColor);
    
    // Draw border
    QColor borderColor;
    if (hasFocus) {
        borderColor = getAccentColor();
    } else {
        borderColor = isDarkMode() ? QColor(100, 100, 100) : QColor(200, 200, 200);
    }
    
    QPen pen(borderColor, hasFocus ? 2 : 1);
    painter->setPen(pen);
    painter->setBrush(Qt::NoBrush);
    painter->drawRoundedRect(rect.adjusted(0, 0, -1, -1), CornerRadius, CornerRadius);
    
    painter->restore();
}

// Helper methods

QColor MysticalStyle::getAccentColor() const
{
#ifdef Q_OS_WIN
    // Try to get system accent color on Windows
    QSettings settings("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\DWM", QSettings::NativeFormat);
    QVariant accentColor = settings.value("AccentColor");
    
    if (accentColor.isValid()) {
        DWORD colorValue = accentColor.toUInt();
        return QColor((colorValue >> 16) & 0xFF, (colorValue >> 8) & 0xFF, colorValue & 0xFF);
    }
#endif
    
    // Fallback to default Windows blue
    return QColor(0, 120, 215);
}

QColor MysticalStyle::getBackgroundColor(bool isDark) const
{
    return isDark ? QColor(32, 32, 32) : QColor(252, 252, 252);
}

QColor MysticalStyle::getForegroundColor(bool isDark) const
{
    return isDark ? QColor(255, 255, 255) : QColor(0, 0, 0);
}

QColor MysticalStyle::getHoverColor(const QColor& baseColor) const
{
    return baseColor.lighter(110);
}

QColor MysticalStyle::getPressedColor(const QColor& baseColor) const
{
    return baseColor.darker(110);
}

void MysticalStyle::drawRoundedRect(QPainter* painter, const QRect& rect, qreal radius, const QColor& color) const
{
    painter->setBrush(color);
    painter->setPen(Qt::NoPen);
    painter->drawRoundedRect(rect, radius, radius);
}

void MysticalStyle::drawGlow(QPainter* painter, const QRect& rect, const QColor& color, qreal radius) const
{
    QRadialGradient gradient(rect.center(), radius);
    gradient.setColorAt(0, color);
    gradient.setColorAt(1, QColor(color.red(), color.green(), color.blue(), 0));
    
    painter->setBrush(gradient);
    painter->setPen(Qt::NoPen);
    painter->drawEllipse(rect.center(), radius, radius);
}

bool MysticalStyle::isDarkMode() const
{
#ifdef Q_OS_WIN
    QSettings settings("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", 
                      QSettings::NativeFormat);
    return settings.value("AppsUseLightTheme", 1).toInt() == 0;
#else
    // Fallback: check application palette
    QPalette palette = QApplication::palette();
    return palette.color(QPalette::Window).lightness() < 128;
#endif
}

bool MysticalStyle::isHighContrast() const
{
#ifdef Q_OS_WIN
    HIGHCONTRAST hc = {};
    hc.cbSize = sizeof(HIGHCONTRAST);
    
    if (SystemParametersInfo(SPI_GETHIGHCONTRAST, sizeof(HIGHCONTRAST), &hc, 0)) {
        return hc.dwFlags & HCF_HIGHCONTRASTON;
    }
#endif
    
    return false;
}