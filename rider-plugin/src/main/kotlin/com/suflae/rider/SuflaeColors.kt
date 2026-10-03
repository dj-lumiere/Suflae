package com.suflae.rider

import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.PlainSyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import com.intellij.platform.lsp.api.customization.LspSemanticTokensSupport
import javax.swing.Icon

/**
 * Suflae's own colors. Each starts as the C# color of its counterpart (record as struct, entity as class,
 * routine as method, module as namespace, global and preset as constant), so Suflae reads like C# in any scheme until
 * Settings | Editor | Color Scheme | Suflae says otherwise. A protocol and an annotation are the
 * exceptions: a protocol starts light blue and italic, an annotation yellow (colorSchemes/).
 */
object SuflaeColors {
    val RECORD = key("SUFLAE_RECORD", TextAttributesKey.find("ReSharper.STRUCT_IDENTIFIER"))
    val ENTITY = key("SUFLAE_ENTITY", DefaultLanguageHighlighterColors.CLASS_NAME)
    val PROTOCOL = key("SUFLAE_PROTOCOL", DefaultLanguageHighlighterColors.INTERFACE_NAME)
    val GENERIC_PARAMETER = key("SUFLAE_GENERIC_PARAMETER", TextAttributesKey.find("ReSharper.TYPE_PARAMETER_IDENTIFIER"))
    val ROUTINE = key("SUFLAE_ROUTINE", DefaultLanguageHighlighterColors.INSTANCE_METHOD)
    val MODULE = key("SUFLAE_MODULE", TextAttributesKey.find("ReSharper.NAMESPACE_IDENTIFIER"))
    val PRESET = key("SUFLAE_PRESET", DefaultLanguageHighlighterColors.CONSTANT)
    val OPERATOR = key("SUFLAE_OPERATOR", DefaultLanguageHighlighterColors.OPERATION_SIGN)
    val ANNOTATION = key("SUFLAE_ANNOTATION", DefaultLanguageHighlighterColors.METADATA)

    private fun key(name: String, csharp: TextAttributesKey) = TextAttributesKey.createTextAttributesKey(name, csharp)
}

/** The colors of the semantic token types the Suflae language server sends that the platform doesn't know. */
internal object SuflaeSemanticTokens : LspSemanticTokensSupport() {
    private val keys = mapOf(
        "recordType" to SuflaeColors.RECORD,
        "entityType" to SuflaeColors.ENTITY,
        "interface" to SuflaeColors.PROTOCOL,
        "typeParameter" to SuflaeColors.GENERIC_PARAMETER,
        "function" to SuflaeColors.ROUTINE,
        "namespace" to SuflaeColors.MODULE,
        "constant" to SuflaeColors.PRESET,
        "operator" to SuflaeColors.OPERATOR,
        "decorator" to SuflaeColors.ANNOTATION,
    )

    override val tokenTypes: List<String> = (super.tokenTypes + keys.keys).distinct()

    override fun getTextAttributesKey(tokenType: String, modifiers: List<String>): TextAttributesKey? =
        keys[tokenType] ?: super.getTextAttributesKey(tokenType, modifiers)
}

/** Settings | Editor | Color Scheme | Suflae. */
class SuflaeColorSettingsPage : ColorSettingsPage {
    private val descriptors = arrayOf(
        AttributesDescriptor("Types//Record, choice, flags, crashable", SuflaeColors.RECORD),
        AttributesDescriptor("Types//Entity", SuflaeColors.ENTITY),
        AttributesDescriptor("Types//Protocol", SuflaeColors.PROTOCOL),
        AttributesDescriptor("Types//Generic parameter", SuflaeColors.GENERIC_PARAMETER),
        AttributesDescriptor("Routine", SuflaeColors.ROUTINE),
        AttributesDescriptor("Module", SuflaeColors.MODULE),
        AttributesDescriptor("Global and preset", SuflaeColors.PRESET),
        AttributesDescriptor("Operator", SuflaeColors.OPERATOR),
        AttributesDescriptor("Annotation", SuflaeColors.ANNOTATION),
    )

    private val tags = mapOf(
        "record" to SuflaeColors.RECORD,
        "entity" to SuflaeColors.ENTITY,
        "protocol" to SuflaeColors.PROTOCOL,
        "generic" to SuflaeColors.GENERIC_PARAMETER,
        "routine" to SuflaeColors.ROUTINE,
        "module" to SuflaeColors.MODULE,
        "preset" to SuflaeColors.PRESET,
        "op" to SuflaeColors.OPERATOR,
        "annotation" to SuflaeColors.ANNOTATION,
    )

    override fun getDisplayName(): String = "Suflae"

    override fun getIcon(): Icon = SuflaeIconProvider.ICON

    override fun getHighlighter(): SyntaxHighlighter = PlainSyntaxHighlighter()

    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = descriptors

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, TextAttributesKey> = tags

    override fun getDemoText(): String = """
        import <module>IO</module>/<module>Console</module>

        global <preset>visits</preset>: <record>Integer</record> = 0

        choice <record>Shape</record>
            CIRCLE
            SQUARE

        record <record>Point</record>
            x: <record>Integer</record>
            y: <record>Integer</record>

        entity <entity>Account</entity> obeys <protocol>Displayable</protocol>
            owner: <record>Text</record>
            history: <entity>List</entity>[<record>Point</record>]

        routine <entity>Account</entity>.<routine>deposit</routine>(amount: <record>Integer</record>)
            me.balance <op>+=</op> amount
            return

        <annotation>@positional</annotation>
        routine <routine>largest</routine>[<generic>T</generic>](items: <entity>List</entity>[<generic>T</generic>]) -> <generic>T</generic>
            return items.<routine>first</routine>()

        routine <routine>start</routine>()
            <preset>visits</preset> <op>+=</op> 1
            <routine>show</routine>(<preset>visits</preset>)
            return
    """.trimIndent()
}
