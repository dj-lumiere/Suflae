package com.suflae.rider

import com.intellij.ide.FileIconProvider
import com.intellij.ide.plugins.PluginManagerCore
import com.intellij.openapi.extensions.PluginId
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.IconLoader
import com.intellij.openapi.vfs.VirtualFile
import org.jetbrains.plugins.textmate.api.TextMateBundleProvider
import javax.swing.Icon

internal const val PLUGIN_ID = "com.suflae.rider"

private val EXTENSIONS = setOf("sf")

internal fun VirtualFile.isSuflae(): Boolean = extension?.lowercase() in EXTENSIONS

/**
 * Registers the TextMate bundle shipped next to the plugin's jar: the grammar from Suflae.tmbundle plus the comment
 * and bracket rules of language-configuration.json. `.sf` files open as TextMate files, highlighted by that grammar.
 */
class SuflaeBundleProvider : TextMateBundleProvider {
    override fun getBundles(): List<TextMateBundleProvider.PluginBundle> {
        val plugin = PluginManagerCore.getPlugin(PluginId.getId(PLUGIN_ID)) ?: return emptyList()
        return listOf(TextMateBundleProvider.PluginBundle("Suflae", plugin.pluginPath.resolve("bundle")))
    }
}

/** Gives `.sf` files the Suflae icon in place of the generic TextMate one. */
class SuflaeIconProvider : FileIconProvider {
    override fun getIcon(file: VirtualFile, flags: Int, project: Project?): Icon? =
        if (file.isSuflae()) ICON else null

    internal companion object {
        val ICON: Icon = IconLoader.getIcon("/icons/suflae.svg", SuflaeIconProvider::class.java)
    }
}
