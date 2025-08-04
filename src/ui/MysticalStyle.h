#pragma once
#include <QProxyStyle>
#include <QStyleOption>
#include <QPainter>
#include <QApplication>

class MysticalStyle : public QProxyStyle
{
    Q_OBJECT
    
public:
    explicit MysticalStyle(QStyle* baseStyle = nullptr);
    
    // QStyle interface
    void drawPrimitive(PrimitiveElement element, const QStyleOption* option,
                       QPainter* painter, const QWidget* widget = nullptr) const override;
    
    void drawControl(ControlElement element, const QStyleOption* option,
                     QPainter* painter, const QWidget* widget = nullptr) const override;
    
    void drawComplexControl(ComplexControl control, const QStyleOptionComplex* option,
                           QPainter* painter, const QWidget* widget = nullptr) const override;
    
    int pixelMetric(PixelMetric metric, const QStyleOption* option = nullptr,
                    const QWidget* widget = nullptr) const override;
    
    QSize sizeFromContents(ContentsType type, const QStyleOption* option,
                          const QSize& size, const QWidget* widget = nullptr) const override;
    
    QRect subElementRect(SubElement element, const QStyleOption* option,
                        const QWidget* widget = nullptr) const override;
    
    QRect subControlRect(ComplexControl control, const QStyleOptionComplex* option,
                        SubControl subControl, const QWidget* widget = nullptr) const override;
    
    int styleHint(StyleHint hint, const QStyleOption* option = nullptr,
                  const QWidget* widget = nullptr, QStyleHintReturn* returnData = nullptr) const override;
    
    QPixmap standardPixmap(StandardPixmap standardPixmap, const QStyleOption* option = nullptr,
                          const QWidget* widget = nullptr) const override;
    
    QPalette standardPalette() const override;
    
private:
    // Windows 11 specific drawing methods
    void drawWindows11Button(const QStyleOption* option, QPainter* painter, const QWidget* widget) const;
    void drawWindows11ScrollBar(const QStyleOptionSlider* option, QPainter* painter, const QWidget* widget) const;
    void drawWindows11TabBar(const QStyleOption* option, QPainter* painter, const QWidget* widget) const;
    void drawWindows11ProgressBar(const QStyleOptionProgressBar* option, QPainter* painter, const QWidget* widget) const;
    void drawWindows11LineEdit(const QStyleOption* option, QPainter* painter, const QWidget* widget) const;
    
    // Helper methods
    QColor getAccentColor() const;
    QColor getBackgroundColor(bool isDark = false) const;
    QColor getForegroundColor(bool isDark = false) const;
    QColor getHoverColor(const QColor& baseColor) const;
    QColor getPressedColor(const QColor& baseColor) const;
    
    void drawRoundedRect(QPainter* painter, const QRect& rect, qreal radius, const QColor& color) const;
    void drawGlow(QPainter* painter, const QRect& rect, const QColor& color, qreal radius = 4.0) const;
    
    bool isDarkMode() const;
    bool isHighContrast() const;
    
    // Windows 11 metrics
    static constexpr int CornerRadius = 4;
    static constexpr int FocusRingWidth = 2;
    static constexpr int ButtonPadding = 8;
    static constexpr int ScrollBarWidth = 12;
    static constexpr int TabBarHeight = 32;
};
