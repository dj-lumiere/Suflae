package com.suflae.rider

import com.intellij.DynamicBundle
import org.jetbrains.annotations.PropertyKey

private const val BUNDLE = "messages.SuflaeBundle"

/** The plugin's text in the IDE's language (messages/SuflaeBundle*.properties). */
internal object SuflaeBundle : DynamicBundle(BUNDLE) {
    fun message(@PropertyKey(resourceBundle = BUNDLE) key: String, vararg params: Any): String = getMessage(key, *params)
}
