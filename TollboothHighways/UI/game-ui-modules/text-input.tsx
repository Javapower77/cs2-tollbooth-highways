// game-ui/common/input/text/text-input.tsx

import React, { useState, useCallback, useRef, useEffect } from 'react';
import { FocusKey, UniqueFocusKey, useUniqueFocusKey, FOCUS_AUTO } from 'cs2/input';
import { UISound } from 'cs2/ui';

export interface TextInputTheme {
    textInput: string;
    input: string;
    focused: string;
    disabled: string;
    placeholder: string;
    error: string;
    prefix: string;
    suffix: string;
}

export interface TextInputProps {
    focusKey?: FocusKey;
    debugName?: string;
    value?: string;
    defaultValue?: string;
    placeholder?: string;
    disabled?: boolean;
    readOnly?: boolean;
    multiline?: boolean;
    rows?: number;
    maxLength?: number;
    minLength?: number;
    type?: 'text' | 'password' | 'email' | 'number' | 'search' | 'tel' | 'url';
    autoComplete?: string;
    autoFocus?: boolean;
    spellCheck?: boolean;
    className?: string;
    theme?: Partial<TextInputTheme>;
    prefix?: React.ReactNode;
    suffix?: React.ReactNode;
    error?: boolean | string;
    selectOnFocus?: boolean;
    clearOnEscape?: boolean;
    submitOnEnter?: boolean;
    sounds?: {
        focus?: UISound | string | null;
        change?: UISound | string | null;
        submit?: UISound | string | null;
    };
    onChange?: (value: string, event?: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
    onFocus?: (event: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
    onBlur?: (event: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
    onKeyDown?: (event: React.KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
    onKeyUp?: (event: React.KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
    onSubmit?: (value: string) => void;
    onClear?: () => void;
    validate?: (value: string) => boolean | string;
}

/**
 * A customizable text input component with focus management, validation, and game UI styling.
 * Supports both single-line input and multi-line textarea modes.
 */
export function TextInput({
    focusKey = FOCUS_AUTO,
    debugName = 'TextInput',
    value,
    defaultValue = '',
    placeholder,
    disabled = false,
    readOnly = false,
    multiline = false,
    rows = 3,
    maxLength,
    minLength,
    type = 'text',
    autoComplete = 'off',
    autoFocus = false,
    spellCheck = false,
    className,
    theme = {},
    prefix,
    suffix,
    error = false,
    selectOnFocus = false,
    clearOnEscape = false,
    submitOnEnter = false,
    sounds = {},
    onChange,
    onFocus,
    onBlur,
    onKeyDown,
    onKeyUp,
    onSubmit,
    onClear,
    validate
}: TextInputProps): JSX.Element {
    const uniqueFocusKey = useUniqueFocusKey(focusKey, debugName);
    const inputRef = useRef<HTMLInputElement | HTMLTextAreaElement>(null);
    
    // Internal state management
    const [internalValue, setInternalValue] = useState(value ?? defaultValue);
    const [isFocused, setIsFocused] = useState(false);
    const [validationError, setValidationError] = useState<string | null>(null);
    
    // Use controlled or uncontrolled pattern
    const currentValue = value !== undefined ? value : internalValue;
    const isControlled = value !== undefined;
    
    // Update internal value when prop changes
    useEffect(() => {
        if (isControlled && value !== internalValue) {
            setInternalValue(value);
        }
    }, [value, isControlled, internalValue]);
    
    // Auto-focus handling
    useEffect(() => {
        if (autoFocus && inputRef.current) {
            inputRef.current.focus();
        }
    }, [autoFocus]);
    
    // Validation
    const validateInput = useCallback((inputValue: string): boolean => {
        if (!validate) return true;
        
        const result = validate(inputValue);
        if (typeof result === 'string') {
            setValidationError(result);
            return false;
        } else if (!result) {
            setValidationError('Invalid input');
            return false;
        } else {
            setValidationError(null);
            return true;
        }
    }, [validate]);
    
    // Event handlers
    const handleChange = useCallback((event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        const newValue = event.target.value;
        
        // Validate input
        validateInput(newValue);
        
        // Update state
        if (!isControlled) {
            setInternalValue(newValue);
        }
        
        // Play sound
        if (sounds.change) {
            // engine.audio.playUISound(sounds.change);
        }
        
        // Call onChange callback
        onChange?.(newValue, event);
    }, [isControlled, onChange, sounds.change, validateInput]);
    
    const handleFocus = useCallback((event: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        setIsFocused(true);
        
        // Select all text on focus if requested
        if (selectOnFocus && event.target) {
            event.target.select();
        }
        
        // Play focus sound
        if (sounds.focus) {
            // engine.audio.playUISound(sounds.focus);
        }
        
        onFocus?.(event);
    }, [selectOnFocus, sounds.focus, onFocus]);
    
    const handleBlur = useCallback((event: React.FocusEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        setIsFocused(false);
        onBlur?.(event);
    }, [onBlur]);
    
    const handleKeyDown = useCallback((event: React.KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>) => {
        // Handle special key combinations
        if (event.key === 'Escape' && clearOnEscape) {
            const newValue = '';
            if (!isControlled) {
                setInternalValue(newValue);
            }
            onChange?.(newValue);
            onClear?.();
            event.preventDefault();
            return;
        }
        
        if (event.key === 'Enter' && submitOnEnter && !multiline) {
            if (validateInput(currentValue)) {
                onSubmit?.(currentValue);
                if (sounds.submit) {
                    // engine.audio.playUISound(sounds.submit);
                }
            }
            event.preventDefault();
            return;
        }
        
        onKeyDown?.(event);
    }, [clearOnEscape, submitOnEnter, multiline, currentValue, isControlled, onChange, onClear, onSubmit, sounds.submit, onKeyDown, validateInput]);
    
    // Build CSS classes
    const cssClasses = [
        theme.textInput || 'text-input',
        className,
        isFocused ? (theme.focused || 'text-input--focused') : null,
        disabled ? (theme.disabled || 'text-input--disabled') : null,
        (error || validationError) ? (theme.error || 'text-input--error') : null,
        readOnly ? 'text-input--readonly' : null,
        multiline ? 'text-input--multiline' : null
    ].filter(Boolean).join(' ');
    
    const inputClasses = [
        theme.input || 'text-input__input'
    ].filter(Boolean).join(' ');
    
    // Common props for both input and textarea
    const commonProps = {
        ref: inputRef,
        className: inputClasses,
        value: currentValue,
        placeholder,
        disabled,
        readOnly,
        maxLength,
        minLength,
        autoComplete,
        spellCheck,
        'data-focus-key': uniqueFocusKey,
        'aria-invalid': !!(error || validationError),
        'aria-describedby': validationError ? `${uniqueFocusKey}-error` : undefined,
        onChange: handleChange,
        onFocus: handleFocus,
        onBlur: handleBlur,
        onKeyDown: handleKeyDown,
        onKeyUp
    };
    
    return (
        <div className={cssClasses} data-testid={debugName}>
            {prefix && (
                <div className={theme.prefix || 'text-input__prefix'}>
                    {prefix}
                </div>
            )}
            
            {multiline ? (
                <textarea
                    {...commonProps as React.TextareaHTMLAttributes<HTMLTextAreaElement>}
                    rows={rows}
                />
            ) : (
                <input
                    {...commonProps as React.InputHTMLAttributes<HTMLInputElement>}
                    type={type}
                />
            )}
            
            {suffix && (
                <div className={theme.suffix || 'text-input__suffix'}>
                    {suffix}
                </div>
            )}
            
            {validationError && (
                <div 
                    id={`${uniqueFocusKey}-error`}
                    className={theme.error || 'text-input__error'}
                    role="alert"
                    aria-live="polite"
                >
                    {validationError}
                </div>
            )}
        </div>
    );
}

// Default theme for styling
export const textInputTheme: TextInputTheme = {
    textInput: 'text-input',
    input: 'text-input__input',
    focused: 'text-input--focused',
    disabled: 'text-input--disabled',
    placeholder: 'text-input__placeholder',
    error: 'text-input--error',
    prefix: 'text-input__prefix',
    suffix: 'text-input__suffix'
};

export default TextInput;