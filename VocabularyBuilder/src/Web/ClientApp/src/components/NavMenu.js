import React, { Component } from 'react';
import { Collapse, Navbar, NavbarBrand, NavbarToggler, NavItem, NavLink, UncontrolledDropdown, DropdownToggle, DropdownMenu, DropdownItem } from 'reactstrap';
import { Link } from 'react-router-dom';
import './NavMenu.css';

export class NavMenu extends Component {
    static displayName = NavMenu.name;

    constructor(props) {
        super(props);

        this.toggleNavbar = this.toggleNavbar.bind(this);
        this.state = {
            collapsed: true,
            currentLanguage: localStorage.getItem('language') || 'en'
        };
    }

    toggleNavbar() {
        this.setState({
            collapsed: !this.state.collapsed
        });
    }

    changeLanguage = (lang) => {
        localStorage.setItem('language', lang);
        this.setState({ currentLanguage: lang });
        // Reload the page to update all components with new language
        window.location.reload();
    }

    render() {
        const languageDisplay = this.state.currentLanguage === 'en' ? '🇬🇧 EN' : '🇫🇷 FR';
        
        return (
            <header>
                <Navbar className="navbar-expand-sm navbar-toggleable-sm ng-white border-bottom box-shadow mb-3" container light>
                    <NavbarBrand tag={Link} to="/">VocabularyBuilder.Web</NavbarBrand>
                    <NavbarToggler onClick={this.toggleNavbar} className="mr-2" />
                    <Collapse className="d-sm-inline-flex flex-sm-row-reverse" isOpen={!this.state.collapsed} navbar>
                        <ul className="navbar-nav flex-grow">
                            <UncontrolledDropdown nav inNavbar>
                                <DropdownToggle nav caret>
                                    {languageDisplay}
                                </DropdownToggle>
                                <DropdownMenu end>
                                    <DropdownItem onClick={() => this.changeLanguage('en')}>
                                        🇬🇧 English
                                    </DropdownItem>
                                    <DropdownItem onClick={() => this.changeLanguage('fr')}>
                                        🇫🇷 French
                                    </DropdownItem>
                                </DropdownMenu>
                            </UncontrolledDropdown>
                            <NavItem>
                                <NavLink tag={Link} className="text-dark" to="/">Home</NavLink>
                            </NavItem>
                            <NavItem>
                                <NavLink tag={Link} className="text-dark" to="/words">Words</NavLink>
                            </NavItem>
                            <NavItem>
                                <NavLink tag={Link} className="text-dark" to="/counter">Counter</NavLink>
                            </NavItem>
                            <NavItem>
                                <NavLink tag={Link} className="text-dark" to="/fetch-data">Fetch data</NavLink>
                            </NavItem>
                            <NavItem>
                                <a className="nav-link text-dark" href="/api">APIshechka</a>
                            </NavItem>
                            <NavItem>
                                <a className="nav-link text-dark" href="/Identity/Account/Manage">Account</a>
                            </NavItem>
                        </ul>
                    </Collapse>
                </Navbar>
            </header>
        );
    }
}
