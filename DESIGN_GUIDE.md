# Guía de Diseño UX/UI - La Casa del Suelo Radiante

## 🎨 Paleta de Colores Profesional

### Colores Principales
- **Primary**: `#2563EB` - Azul corporativo principal
- **PrimaryDark**: `#1D4ED8` - Azul oscuro para botones pressed/hover
- **Secondary**: `#EFF6FF` - Azul muy claro para fondos secundarios
- **Tertiary**: `#3B82F6` - Azul intermedio para acentos

### Colores de Estado
- **Success**: `#10B981` - Verde para estados exitosos
- **Warning**: `#F59E0B` - Amarillo para advertencias
- **Error**: `#EF4444` - Rojo para errores
- **Info**: `#3B82F6` - Azul para información

### Escala de Grises Moderna
- **Gray50**: `#F9FAFB` - Fondo muy claro
- **Gray100**: `#F3F4F6` - Fondo de cards
- **Gray200**: `#E5E7EB` - Bordes suaves
- **Gray300**: `#D1D5DB` - Bordes normales
- **Gray400**: `#9CA3AF` - Placeholders
- **Gray500**: `#6B7280` - Texto secundario
- **Gray600**: `#4B5563` - Texto normal
- **Gray700**: `#374151` - Texto importante
- **Gray800**: `#1F2937` - Fondos oscuros
- **Gray900**: `#111827` - Texto principal
- **Gray950**: `#030712` - Negro profundo

## 📱 Estilos de Botones

### Botón Principal
```xml
<Button Text="Acción Principal" 
        Style="{StaticResource PrimaryButton}" />
```
- Fondo azul primario
- Texto blanco
- Sombra sutil
- Bordes redondeados (12px)

### Botón Secundario
```xml
<Button Text="Acción Secundaria" 
        Style="{StaticResource SecondaryButton}" />
```
- Fondo azul claro
- Texto azul primario
- Borde azul
- Sin sombra

### Botón Outline
```xml
<Button Text="Acción Terciaria" 
        Style="{StaticResource OutlineButton}" />
```
- Fondo transparente
- Texto azul primario
- Borde azul
- Hover effect

## 🎯 Estilos de Texto

### Títulos de Página
```xml
<Label Text="Título Principal" 
       Style="{StaticResource PageTitle}" />
```
- Tamaño: 28px
- Peso: Bold
- Margen optimizado
- Centrado

### Títulos de Sección
```xml
<Label Text="Sección" 
       Style="{StaticResource SectionTitle}" />
```
- Tamaño: 20px
- Peso: Bold
- Color: Primary
- Margen: 16px top, 8px bottom

### Texto del Cuerpo
```xml
<Label Text="Contenido normal" 
       Style="{StaticResource BodyText}" />
```
- Tamaño: 16px
- Interlineado: 1.4
- Color adaptable al tema

### Texto Pequeño
```xml
<Label Text="Información adicional" 
       Style="{StaticResource Caption}" />
```
- Tamaño: 12px
- Color gris
- Para metadatos y información secundaria

## 📝 Componentes de Formulario

### Entrada de Texto Profesional
```xml
<Entry Placeholder="Ingrese texto" 
       Style="{StaticResource FormEntry}" />
```
- Fondo gris claro
- Altura mínima: 48px
- Bordes redondeados
- Placeholder con color apropiado

### Barra de Búsqueda
```xml
<SearchBar Placeholder="Buscar..." 
           Style="{StaticResource ProfessionalSearchBar}" />
```
- Diseño consistente
- Colores adaptables al tema
- Tipografía profesional

## 🎨 Contenedores y Cards

### Card Principal
```xml
<Frame Style="{StaticResource CardFrame}">
    <VerticalStackLayout>
        <!-- Contenido -->
    </VerticalStackLayout>
</Frame>
```
- Bordes redondeados (16px)
- Sombra condicional (no en iOS/Mac)
- Padding generoso (20px)
- Fondo adaptable al tema

## 🌓 Soporte para Tema Claro/Oscuro

Todos los estilos incluyen soporte completo para temas claros y oscuros usando `AppThemeBinding`:

```xml
TextColor="{AppThemeBinding Light={StaticResource Gray900}, Dark={StaticResource White}}"
```

## 📐 Principios de Diseño

### Espaciado Consistente
- **Padding**: 16px, 20px, 24px
- **Margins**: 8px, 16px, 24px
- **Spacing**: 12px, 16px, 20px

### Jerarquía Visual
1. **Títulos**: PageTitle (28px, Bold)
2. **Subtítulos**: SectionTitle (20px, Bold, Primary)
3. **Contenido**: BodyText (16px, Regular)
4. **Metadatos**: Caption (12px, Gray)

### Accesibilidad
- Altura mínima de botones: 44px (recomendación iOS/Android)
- Contraste adecuado para todos los textos
- Tamaños de fuente legibles
- Áreas de toque apropiadas

## 🚀 Implementación en Páginas

### Estructura Recomendada
```xml
<ContentPage BackgroundColor="{AppThemeBinding Light={StaticResource Gray50}, Dark={StaticResource Gray900}}">
    <ScrollView>
        <VerticalStackLayout Padding="20" Spacing="24">
            <!-- Título -->
            <Label Text="Título de Página" Style="{StaticResource PageTitle}" />
            
            <!-- Contenido en Cards -->
            <Frame Style="{StaticResource CardFrame}">
                <VerticalStackLayout Spacing="16">
                    <Label Text="Sección" Style="{StaticResource SectionTitle}" />
                    <!-- Contenido -->
                </VerticalStackLayout>
            </Frame>
            
            <!-- Botones de Acción -->
            <Button Text="Acción Principal" Style="{StaticResource PrimaryButton}" />
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

## 📱 Compatibilidad Multiplataforma

Los estilos están optimizados para:
- ✅ **Android** - Material Design principles
- ✅ **iOS** - Human Interface Guidelines
- ✅ **Windows** - Fluent Design
- ✅ **macOS** - macOS Design Guidelines

### Consideraciones Específicas
- **Sombras**: Deshabilitadas en iOS/Mac para seguir guidelines nativos
- **Bordes**: Adaptados según plataforma
- **Espaciado**: Optimizado para diferentes densidades de pantalla

## 🎯 Mejores Prácticas

### ✅ Hacer
- Usar estilos predefinidos siempre que sea posible
- Mantener consistencia en espaciado
- Aplicar jerarquía visual clara
- Probar en tema claro y oscuro

### ❌ Evitar
- Colores hardcodeados en XAML
- Tamaños de fuente arbitrarios
- Espaciado inconsistente
- Elementos sin accesibilidad

## 🔧 Extensibilidad

Para crear nuevos estilos, sigue el patrón establecido:

```xml
<Style x:Key="MiEstiloPersonalizado" TargetType="Button">
    <Setter Property="BackgroundColor" Value="{StaticResource Primary}" />
    <Setter Property="TextColor" Value="White" />
    <Setter Property="FontFamily" Value="OpenSansRegular" />
    <Setter Property="CornerRadius" Value="12" />
    <!-- Más propiedades -->
</Style>
```

---

**Nota**: Esta guía garantiza una experiencia de usuario consistente, profesional y accesible en todas las plataformas soportadas por .NET MAUI.
