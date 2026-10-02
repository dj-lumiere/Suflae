package com.suflae.rider

import com.intellij.openapi.components.BaseState
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.SimplePersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.components.service
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.options.BoundConfigurable
import com.intellij.openapi.project.ProjectManager
import com.intellij.openapi.ui.DialogPanel
import com.intellij.ui.dsl.builder.AlignX
import com.intellij.ui.dsl.builder.bindText
import com.intellij.ui.dsl.builder.panel

/** Which Suflae language server to run. Empty means the workspace dev build (see `locateServer`). */
@Service(Service.Level.APP)
@State(name = "SuflaeSettings", storages = [Storage("suflae.xml")])
internal class SuflaeSettings : SimplePersistentStateComponent<SuflaeSettings.Options>(Options()) {
    class Options : BaseState() {
        var serverPath by string()
    }

    var serverPath: String
        get() = state.serverPath.orEmpty()
        set(value) {
            state.serverPath = value.trim().ifEmpty { null }
        }

    companion object {
        fun getInstance(): SuflaeSettings = service()
    }
}

/** Settings | Languages & Frameworks | Suflae. */
internal class SuflaeConfigurable : BoundConfigurable("Suflae") {
    private val settings = SuflaeSettings.getInstance()

    override fun createPanel(): DialogPanel = panel {
        row("Language server:") {
            textFieldWithBrowseButton(FileChooserDescriptorFactory.singleFile().withTitle("Suflae Language Server"))
                .bindText(settings::serverPath)
                .align(AlignX.FILL)
                .comment(
                    "Suflae.dll (run with dotnet) or a Suflae executable. Empty uses the dev build, " +
                        "&lt;project&gt;/Suflae/bin/Debug/net10.0/Suflae.dll."
                )
        }
    }

    override fun apply() {
        super.apply()
        ProjectManager.getInstance().openProjects.forEach(SuflaeLanguageServer::restart)
    }
}
