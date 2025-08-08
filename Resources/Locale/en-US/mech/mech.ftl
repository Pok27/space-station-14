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
mech-integrity-display = Integrity: {$amount}%
mech-energy-display = Energy: {$amount}%
mech-energy-missing = Energy: MISSING
mech-equipment-slot-display = Equipment: {$used}/{$max} used
mech-module-slot-display = Modules: {$used}/{$max} used

# Atmospheric system
mech-air-cabin-label = Cabin air:
mech-air-toggle = Toggle Cabin
mech-cabin-gas-label = Cabin Pressure:
mech-cabin-gas-level = { $state ->
    [ok] {$level} kPa
    *[na] N/A
}

# Fan system
mech-fan-label = Fan:
mech-fan-on = On
mech-fan-off = Off
mech-fan-toggle = Toggle Fan
mech-fan-status = Status: { $state ->
    [idle] Idle
    [on] On
    *[off] Off
}
mech-fan-missing = No fan module installed

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

mech-lock-dna-info = DNA: {$dna}
mech-lock-card-info = ID: {$name}
mech-lock-not-set = Not set
