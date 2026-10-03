package com.suflae.rider

import com.intellij.lang.Language
import com.intellij.openapi.fileTypes.PlainSyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import org.jetbrains.plugins.textmate.TextMateService
import org.jetbrains.plugins.textmate.language.syntax.highlighting.TextMateHighlighter
import org.jetbrains.plugins.textmate.language.syntax.lexer.TextMateHighlightingLexer

/**
 * Suflae as a language Rider can name. Files stay with the TextMate grammar; this is what a hover's ```suflae code block
 * is highlighted as, since Rider colors a code block only for a registered language with a highlighter.
 */
object SuflaeLanguage : Language("suflae") {
    private fun readResolve(): Any = SuflaeLanguage

    override fun getDisplayName(): String = "Suflae"

    /** A language is registered when its object loads: called before anything can ask for it. */
    fun ensureRegistered() = Unit
}

/** Highlights Suflae code outside a file (a hover's code block) with the same TextMate grammar the editor uses. */
class SuflaeSyntaxHighlighterFactory : SyntaxHighlighterFactory() {
    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        TextMateService.getInstance().getLanguageDescriptorByExtension("sf")
            ?.let { TextMateHighlighter(TextMateHighlightingLexer(it, LINE_LIMIT)) }
            ?: PlainSyntaxHighlighter()

    private companion object {
        /** Longer lines than this are left plain, as TextMate leaves them in the editor. */
        const val LINE_LIMIT = 10_000
    }
}
