# UI
mech-menu-title = mech control panel
mech-equipment-label = Equipment
mech-modules-label = Modules

# Verbs
mech-verb-enter = Enter
mech-verb-exit = Remove pilot
mech-ui-open-verb = Open control panel

# Equipment installation
mech-equipment-begin-install-popup = Installing the {THE($item)}...
mech-equipment-finish-install-popup = Finished installing the {THE($item)}

# Equipment selection
mech-equipment-select-popup = {$item} selected
mech-equipment-select-none-popup = Nothing selected

# Radial menu
mech-radial-no-equipment = No Equipment

# Status displays
mech-integrity-display-label = Integrity
mech-integrity-display = {$amount} %
mech-integrity-display-critical = CRITICAL
mech-energy-display-label = Energy
mech-energy-display = {$amount} %
mech-energy-missing = MISSING
mech-equipment-slot-display = Equipment: {$used}/{$max} used
mech-module-slot-display = Modules: {$used}/{$max} used
mech-grabber-capacity = {$current}/{$max}

# Atmospheric system
mech-cabin-pressure-label = Cabin Pressure:
mech-cabin-pressure-level = {$level} kPa
mech-cabin-temperature-label = Temperature:
mech-cabin-temperature-level = {$tempC} °C
mech-air-toggle = Toggle
mech-cabin-purge = Purge

mech-tank-pressure-label = Tank Pressure:
mech-tank-pressure-level = { $state ->
    [ok] {$pressure} kPa
    *[na] N/A
}

# Fan system
mech-fan-label = Fan:
mech-fan-on = On
mech-fan-off = Off
mech-fan-toggle = Toggle Fan
mech-fan-status-label = Fan Status:
mech-fan-status = { $state ->
    [on] On
    [idle] Idle
    [off] Off
    *[na] N/A
}
mech-fan-missing = No fan module
mech-filter-enabled = Filter

# Access restriction
mech-no-enter-popup = You cannot pilot this.

# Alert
mech-eject-pilot-alert-popup = {$user} is pulling the pilot out of the {$item}!

# Installation constraint
mech-install-blocked-pilot-popup = Cannot install while a pilot is inside!
mech-remove-blocked-pilot-popup = Cannot remove while a pilot is inside!
mech-capacity-equipment-full-popup = No free equipment slots.
mech-capacity-modules-full-popup = No free module capacity.
mech-duplicate-equipment-popup = Identical equipment already installed.
mech-duplicate-module-popup = Identical module already installed.

# Lock system
mech-lock-dna-label = DNA Lock:
mech-lock-card-label = ID Lock:

mech-lock-register = Register Lock
mech-lock-activate = Activate
mech-lock-deactivate = Deactivate
mech-lock-reset = Reset

mech-lock-no-dna-popup = You don't have DNA to lock with!
mech-lock-no-card-popup = You don't have an ID card to lock with!
mech-lock-access-denied-popup = Access denied! This mech is locked.

mech-lock-dna-registered-popup = DNA lock registered!
mech-lock-card-registered-popup = ID lock registered!
mech-lock-activated-popup = Lock activated!
mech-lock-deactivated-popup = Lock deactivated!
mech-lock-reset-success-popup = Lock reset!

mech-lock-dna-info = [{$dna}]
mech-lock-card-info = [{$name}]
mech-lock-not-set = Not set

# Settings access banner
mech-settings-no-access = Access denied. You do not have permission to change settings.
mech-remove-disabled-tooltip = Cannot remove while a pilot is inside.

# Critical state messages
mech-cannot-insert-critical = You cannot insert anything while the mech is in critical state.
